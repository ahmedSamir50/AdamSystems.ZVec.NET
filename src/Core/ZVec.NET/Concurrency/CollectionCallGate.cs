namespace ZVec.NET.Concurrency;

/// <summary>
/// Coordinates factory-wide native-call throttling and optional per-collection read limits.
/// Ordering: acquire the factory native gate first, then the read gate (read paths only).
/// </summary>
internal sealed class CollectionCallGate
{
    private readonly ZVecFactory _factory;
    private readonly CancellationToken _factoryShutdownToken;
    private readonly SemaphoreSlim? _readGate;

    public CollectionCallGate(
        ZVecFactory factory,
        CancellationToken factoryShutdownToken,
        SemaphoreSlim? readGate)
    {
        _factory = factory;
        _factoryShutdownToken = factoryShutdownToken;
        _readGate = readGate;
    }

    /// <summary>
    /// True when an async path must <see cref="EnterNativeCallAsync"/> (factory throttle enabled).
    /// When false, sync enter is a no-op and async wrappers may call sync ops without allocation.
    /// </summary>
    public bool NeedsAsyncWaitForNative => _factory.HasNativeCallGate;

    /// <summary>
    /// True when an async read path must <see cref="EnterReadAsync"/> (factory and/or read throttle).
    /// </summary>
    public bool NeedsAsyncWaitForRead => _factory.HasNativeCallGate || _readGate is not null;

    public void EnterNativeCall()
    {
        _factory.EnterNativeCall(_factoryShutdownToken);
    }

    public void ExitNativeCall()
    {
        _factory.ExitNativeCall();
    }

    public ValueTask EnterNativeCallAsync(CancellationToken operationToken = default)
    {
        if (!_factory.HasNativeCallGate)
            return ValueTask.CompletedTask;

        return EnterNativeCallAsyncCore(operationToken);
    }

    private async ValueTask EnterNativeCallAsyncCore(CancellationToken operationToken)
    {
        if (!operationToken.CanBeCanceled)
        {
            await _factory.EnterNativeCallAsync(_factoryShutdownToken).ConfigureAwait(false);
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, _factoryShutdownToken);
        await _factory.EnterNativeCallAsync(linked.Token).ConfigureAwait(false);
    }

    public void EnterRead()
    {
        EnterNativeCall();
        try
        {
            _readGate?.Wait(_factoryShutdownToken);
        }
        catch
        {
            ExitNativeCall();
            throw;
        }
    }

    public void ExitRead()
    {
        try
        {
            _readGate?.Release();
        }
        finally
        {
            ExitNativeCall();
        }
    }

    public ValueTask EnterReadAsync(CancellationToken operationToken = default)
    {
        if (!NeedsAsyncWaitForRead)
            return ValueTask.CompletedTask;

        return EnterReadAsyncCore(operationToken);
    }

    private async ValueTask EnterReadAsyncCore(CancellationToken operationToken)
    {
        await EnterNativeCallAsync(operationToken).ConfigureAwait(false);
        if (_readGate is null)
            return;

        try
        {
            if (!operationToken.CanBeCanceled)
            {
                await _readGate.WaitAsync(_factoryShutdownToken).ConfigureAwait(false);
            }
            else
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(operationToken, _factoryShutdownToken);
                await _readGate.WaitAsync(linked.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            ExitNativeCall();
            throw;
        }
    }

    public void DisposeReadGate()
    {
        _readGate?.Dispose();
    }
}
