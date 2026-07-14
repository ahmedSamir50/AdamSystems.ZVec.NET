namespace AdamSystems.ZVec.NET.Concurrency;

/// <summary>
/// A write-preferring, non-reentrant reader-writer lock that supports both
/// synchronous and asynchronous callers without sync-over-async.
/// <para>
/// <b>Scope:</b> This lock is intended only for purely managed shared state that
/// genuinely has concurrent readers and exclusive writers (e.g. a plugin registry
/// or a schema cache). It is <b>NOT</b> used for native library lifecycle management
/// (factory init/shutdown, collection open/close/destroy), which is handled by native
/// atomics (<c>std::atomic&lt;bool&gt;</c> inside <c>GlobalConfig</c>) and by
/// <see cref="System.Threading.Interlocked"/> on the managed side.
/// </para>
/// <para>
/// Synchronous callers block via <c>Monitor.Wait</c> — no thread-pool threads are
/// consumed while waiting. Asynchronous callers are queued via
/// <see cref="TaskCompletionSource{T}"/> with <c>RunContinuationsAsynchronously</c>.
/// </para>
/// </summary>
public sealed class AsyncReaderWriterLock
{
    private readonly object _sync = new();
    private int _readersActive = 0;
    private bool _writerActive = false;
    private int _writersWaiting = 0;
    
    private readonly Queue<TaskCompletionSource<IDisposable>> _readerQueue = new();
    private readonly Queue<TaskCompletionSource<IDisposable>> _writerQueue = new();

    private readonly AsyncLocal<bool> _isWriteLockHeld = new();

    public ValueTask<IDisposable> EnterReadAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<IDisposable>(cancellationToken);
        }

        lock (_sync)
        {
            if (!_writerActive && _writersWaiting == 0)
            {
                _readersActive++;
                return new ValueTask<IDisposable>(new ReaderReleaser(this));
            }
            
            var tcs = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
            _readerQueue.Enqueue(tcs);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => 
                {
                    lock (_sync)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                    }
                });
            }
            return new ValueTask<IDisposable>(tcs.Task);
        }
    }

    /// <summary>
    /// Enters a read lock synchronously, blocking via <c>Monitor.Wait</c> (not
    /// sync-over-async) until a read lock is available or the token is cancelled.
    /// </summary>
    public IDisposable EnterRead(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        lock (_sync)
        {
            // Fast path: no active writer and no writers waiting — enter immediately.
            if (!_writerActive && _writersWaiting == 0)
            {
                _readersActive++;
                return new ReaderReleaser(this);
            }

            // Slow path: block the calling thread via Monitor.Wait.
            // This does NOT consume a thread-pool thread while waiting.
            while (_writerActive || _writersWaiting > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
                Monitor.Wait(_sync);
            }

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            _readersActive++;
            return new ReaderReleaser(this);
        }
    }

    public ValueTask<IDisposable> EnterWriteAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<IDisposable>(cancellationToken);
        }

        if (_isWriteLockHeld.Value)
        {
            throw new InvalidOperationException(ZVecDefaults.Errors.WriteLockReentrancyNotSupported);
        }

        TaskCompletionSource<IDisposable> tcs;
        lock (_sync)
        {
            if (!_writerActive && _readersActive == 0)
            {
                _writerActive = true;
                _isWriteLockHeld.Value = true;
                return new ValueTask<IDisposable>(new WriterReleaser(this));
            }
            
            _writersWaiting++;
            tcs = new TaskCompletionSource<IDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
            _writerQueue.Enqueue(tcs);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => 
                {
                    lock (_sync)
                    {
                        if (tcs.TrySetCanceled(cancellationToken))
                        {
                            _writersWaiting--;
                        }
                    }
                });
            }
        }
        
        return new ValueTask<IDisposable>(tcs.Task.ContinueWith(t => 
        {
            if (t.Status == TaskStatus.RanToCompletion)
            {
                _isWriteLockHeld.Value = true;
                return t.Result;
            }
            t.GetAwaiter().GetResult(); // Throw if canceled/faulted
            return null!;
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
    }

    /// <summary>
    /// Enters a write lock synchronously, blocking via <c>Monitor.Wait</c> (not
    /// sync-over-async) until exclusive access is available or the token is cancelled.
    /// </summary>
    public IDisposable EnterWrite(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException(cancellationToken);

        if (_isWriteLockHeld.Value)
            throw new InvalidOperationException(ZVecDefaults.Errors.WriteLockReentrancyNotSupported);

        lock (_sync)
        {
            // Fast path: no active writer and no active readers — enter immediately.
            if (!_writerActive && _readersActive == 0)
            {
                _writerActive = true;
                _isWriteLockHeld.Value = true;
                return new WriterReleaser(this);
            }

            // Slow path: block the calling thread via Monitor.Wait.
            _writersWaiting++;
            try
            {
                while (_writerActive || _readersActive > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw new OperationCanceledException(cancellationToken);
                    Monitor.Wait(_sync);
                }

                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                _writersWaiting--;
            }

            _writerActive = true;
            _isWriteLockHeld.Value = true;
            return new WriterReleaser(this);
        }
    }

    private void ReleaseReader()
    {
        TaskCompletionSource<IDisposable>? writerToWake = null;
        lock (_sync)
        {
            _readersActive--;
            if (_readersActive == 0 && _writersWaiting > 0)
            {
                // Wake async writers waiting via TCS.
                while (_writerQueue.Count > 0)
                {
                    var tcs = _writerQueue.Dequeue();
                    if (!tcs.Task.IsCompleted)
                    {
                        _writersWaiting--;
                        _writerActive = true;
                        writerToWake = tcs;
                        break;
                    }
                }
            }
            // Wake any sync callers blocked in Monitor.Wait.
            Monitor.PulseAll(_sync);
        }
        writerToWake?.TrySetResult(new WriterReleaser(this));
    }
    
    private void ReleaseWriter()
    {
        _isWriteLockHeld.Value = false;
        
        TaskCompletionSource<IDisposable>? writerToWake = null;
        List<TaskCompletionSource<IDisposable>>? readersToWake = null;
        
        lock (_sync)
        {
            _writerActive = false;
            if (_writersWaiting > 0)
            {
                // Wake the next async writer waiting via TCS.
                while (_writerQueue.Count > 0)
                {
                    var tcs = _writerQueue.Dequeue();
                    if (!tcs.Task.IsCompleted)
                    {
                        _writersWaiting--;
                        _writerActive = true;
                        writerToWake = tcs;
                        break;
                    }
                }
            }
            else if (_readerQueue.Count > 0)
            {
                // Wake all async readers waiting via TCS.
                readersToWake = new List<TaskCompletionSource<IDisposable>>();
                while (_readerQueue.Count > 0)
                {
                    var tcs = _readerQueue.Dequeue();
                    if (!tcs.Task.IsCompleted)
                    {
                        _readersActive++;
                        readersToWake.Add(tcs);
                    }
                }
            }
            // Wake any sync callers blocked in Monitor.Wait.
            Monitor.PulseAll(_sync);
        }
        
        writerToWake?.TrySetResult(new WriterReleaser(this));
        if (readersToWake != null)
        {
            foreach (var r in readersToWake)
            {
                r.TrySetResult(new ReaderReleaser(this));
            }
        }
    }
    
    private sealed class ReaderReleaser : IDisposable
    {
        private readonly AsyncReaderWriterLock _parent;
        private int _disposed;
        public ReaderReleaser(AsyncReaderWriterLock parent) => _parent = parent;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _parent.ReleaseReader();
            }
        }
    }
    
    private sealed class WriterReleaser : IDisposable
    {
        private readonly AsyncReaderWriterLock _parent;
        private int _disposed;
        public WriterReleaser(AsyncReaderWriterLock parent) => _parent = parent;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _parent.ReleaseWriter();
            }
        }
    }
}
