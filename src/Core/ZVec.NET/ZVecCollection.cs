using ZVec.NET.Internal;
using ZVec.NET.Interop;
using ZVec.NET.Query;
using System.Runtime.InteropServices;

namespace ZVec.NET;

/// <summary>
/// Managed wrapper around a native ZVec collection handle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispose vs Destroy semantics:</b><br/>
/// <see cref="Dispose"/>/<see cref="DisposeAsync"/> → <c>zvec_collection_close</c> (data is preserved).<br/>
/// <see cref="Destroy"/>/<see cref="DestroyAsync"/> → <c>zvec_collection_destroy</c> then close (data is deleted).
/// </para>
/// <para>
/// <b>Thread safety:</b> Idempotency and mutual exclusion for all disposal paths are achieved
/// with <see cref="Interlocked.Exchange(ref int, ref int)"/> on <c>int</c> flags — no custom lock.
/// This mirrors the BCL <see cref="SafeHandle"/> pattern.
/// </para>
/// </remarks>
public sealed class ZVecCollection : IZvecCollection
{
    // Raw native handle — we manage it manually (not via SafeHandle) so we can
    // control close-vs-destroy ordering precisely.
    private readonly nint _handle;

    // 0 = alive, 1 = disposed. Interlocked.Exchange guarantees exactly-once close
    // even under concurrent Dispose + DisposeAsync.
    private int _disposed;

    // 0 = not destroyed, 1 = destroyed. Prevents double-destroy.
    private int _destroyed;

    /// <inheritdoc/>
    public string Path { get; }

    /// <inheritdoc/>
    public ZVecCollectionSchema? Schema { get; private set; }

    /// <inheritdoc/>
    public ZVecCollectionOptions Options => _options;

    /// <inheritdoc/>
    public ZVecCollectionStats Stats => GetStats();

    // Token cancelled when ZVecFactory.Shutdown() is called.
    private readonly CancellationToken _factoryShutdownToken;
    private readonly ZVecFactory _factory;
    private readonly SemaphoreSlim? _readGate;
    private readonly ZVecCollectionOptions _options;

    internal ZVecCollection(
        nint handle,
        string path,
        ZVecCollectionSchema? schema,
        CancellationToken factoryShutdownToken,
        ZVecFactory factory,
        ZVecCollectionOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle, nameof(handle));
        _handle = handle;
        Path = path;
        Schema = schema;
        _factoryShutdownToken = factoryShutdownToken;
        _factory = factory;
        _options = options ?? new ZVecCollectionOptions();

        int maxReads = _options.MaxConcurrentReads;
        if (maxReads > 0)
            _readGate = new SemaphoreSlim(maxReads, maxReads);
    }

    private void EnterNativeCall()
    {
        _factory.EnterNativeCall(_factoryShutdownToken);
    }

    private void ExitNativeCall()
    {
        _factory.ExitNativeCall();
    }

    private void EnterRead()
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

    private void ExitRead()
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

    // =========================================================================
    // Lifecycle — Dispose / DisposeAsync
    // =========================================================================

    /// <summary>
    /// Closes the collection. Idempotent — safe to call multiple times and
    /// concurrently with <see cref="DisposeAsync"/>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return; // Already closed — no-op.

        // Capture result but don't throw during disposal per .NET conventions.
        // Only call native close if the factory is initialized — otherwise the native
        // library resources are invalid and calling close would cause an Access Violation.
        // This mirrors the documented guard in SafeZvecHandle.ReleaseHandle().
        if (_factory.IsInitialized)
            _ = NativeMethods.zvec_collection_close(_handle);
        _factory.OpenCollections.TryRemove(_handle, out _);
        _readGate?.Dispose();
    }

    /// <summary>
    /// Asynchronously closes the collection. Idempotent.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Lifecycle — Destroy / DestroyAsync
    // =========================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>zvec_collection_destroy</c> (deletes on-disk data) first, then
    /// <c>zvec_collection_close</c>. The destroy → close ordering is required by the
    /// native C API contract. Subsequent calls (including <see cref="Dispose"/>) are no-ops.
    /// </remarks>
    public void Destroy()
    {
        // Guard: only one caller can execute the destroy path.
        if (Interlocked.Exchange(ref _destroyed, 1) != 0)
            return; // Already destroyed — no-op.

        // Atomically claim the right to clean up the native handle.
        // If Dispose() already ran, we cannot safely call native destroy.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_factory.IsInitialized)
            {
                _ = NativeMethods.zvec_collection_destroy(_handle);
                _ = NativeMethods.zvec_collection_close(_handle);
            }
            _factory.OpenCollections.TryRemove(_handle, out _);
        }
    }

    /// <inheritdoc/>
    public ValueTask DestroyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Destroy();
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
        {
            throw new ObjectDisposedException(nameof(ZVecCollection));
        }
    }

    // =========================================================================
    // Epic E11 — Stats
    // =========================================================================

    /// <inheritdoc/>
    public ZVecCollectionStats GetStats()
    {
        ThrowIfDisposed();

        EnterRead();
        try
        {
            int rc = NativeMethods.zvec_collection_get_stats(_handle, out nint statsPtr);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(GetStats));

            try
            {
                ulong docCount = NativeMethods.zvec_collection_stats_get_doc_count(statsPtr);
                nuint indexCount = NativeMethods.zvec_collection_stats_get_index_count(statsPtr);
                var completeness = new Dictionary<string, float>((int)indexCount);

                for (nuint i = 0; i < indexCount; i++)
                {
                    nint namePtr = NativeMethods.zvec_collection_stats_get_index_name(statsPtr, i);
                    string name = namePtr != IntPtr.Zero
                        ? Marshal.PtrToStringUTF8(namePtr) ?? string.Empty
                        : string.Empty;
                    completeness[name] = NativeMethods.zvec_collection_stats_get_index_completeness(statsPtr, i);
                }

                return new ZVecCollectionStats
                {
                    DocCount = (long)docCount,
                    IndexCompleteness = completeness
                };
            }
            finally
            {
                NativeMethods.zvec_collection_stats_destroy(statsPtr);
            }
        }
        finally
        {
            ExitRead();
        }
    }

    // =========================================================================
    // Epic E12 — CRUD
    // =========================================================================

    public ZVecStatus Insert(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);

        EnterNativeCall();
        try
        {
            using var builder = NativeDocBuilder.Build(doc);
            nint handle = builder.Handle;
            unsafe
            {
                nint* ptr = &handle;
                var rc = NativeMethods.zvec_collection_insert(
                    _handle, (nint)ptr, 1, out nuint success, out nuint error);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Insert));
                return new ZVecStatus { Code = (ZVecErrorCode)rc };
            }
        }
        finally
        {
            ExitNativeCall();
        }
    }

    public ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_insert(
                        _handle, (nint)p, (nuint)docs.Length, out nuint success, out nuint error);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Insert));
                    return new ZVecStatus { Code = (ZVecErrorCode)rc };
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
        }
    }

    public IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return [];

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_insert_with_results(
                        _handle, (nint)p, (nuint)docs.Length, out nint resultsPtr, out nuint resCount);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(InsertWithResults));
                    return UnmarshalWriteResults(resultsPtr, resCount);
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
        }
    }

    public ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Insert(doc));
    }

    public ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(InsertWithResults(arr));
        return ValueTask.FromResult(InsertWithResults(docs.ToArray()));
    }

    public ZVecStatus Update(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);
        return Update([doc]);
    }

    public ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        EnterNativeCall();
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_update(
                        _handle, (nint)p, (nuint)docs.Length, out nuint success, out nuint error);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Update));
                    return new ZVecStatus { Code = (ZVecErrorCode)rc };
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            ExitNativeCall();
        }
    }

    public IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return [];

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        EnterNativeCall();
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_update_with_results(
                        _handle, (nint)p, (nuint)docs.Length, out nint resultsPtr, out nuint resCount);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(UpdateWithResults));
                    return UnmarshalWriteResults(resultsPtr, resCount);
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            ExitNativeCall();
        }
    }

    public ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Update(doc));
    }

    public ValueTask<ZVecStatus> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(Update(arr));
        return ValueTask.FromResult(Update(docs.ToArray()));
    }

    public ZVecStatus Upsert(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);
        return Upsert([doc]);
    }

    public ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        EnterNativeCall();
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_upsert(
                        _handle, (nint)p, (nuint)docs.Length, out nuint success, out nuint error);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Upsert));
                    return new ZVecStatus { Code = (ZVecErrorCode)rc };
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            ExitNativeCall();
        }
    }

    public IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        ThrowIfDisposed();
        if (docs.IsEmpty) return [];

        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = new nint[docs.Length];
        EnterNativeCall();
        try
        {
            for (int i = 0; i < docs.Length; i++)
            {
                builders[i] = NativeDocBuilder.Build(docs[i]);
                ptrs[i] = builders[i].Handle;
            }

            unsafe
            {
                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_upsert_with_results(
                        _handle, (nint)p, (nuint)docs.Length, out nint resultsPtr, out nuint resCount);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(UpsertWithResults));
                    return UnmarshalWriteResults(resultsPtr, resCount);
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            ExitNativeCall();
        }
    }

    public ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Upsert(doc));
    }

    public ValueTask<ZVecStatus> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(docs);
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(Upsert(arr));
        return ValueTask.FromResult(Upsert(docs.ToArray()));
    }

    public ZVecStatus Delete(string pk)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);

        unsafe
        {
            // CRITICAL: We MUST append \0 before GetBytes because the native C API expects null-terminated const char*.
            var utf8Pk = System.Text.Encoding.UTF8.GetBytes(pk + "\0");
            fixed (byte* pPk = utf8Pk)
            {
                nint* pArray = stackalloc nint[] { (nint)pPk };
                var rc = NativeMethods.zvec_collection_delete(
                    _handle, (nint)pArray, 1, out nuint success, out nuint error);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Delete));
                return new ZVecStatus { Code = (ZVecErrorCode)rc };
            }
        }
    }

    public ZVecStatus Delete(ReadOnlySpan<string> pks)
    {
        ThrowIfDisposed();
        if (pks.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };

        unsafe
        {
            var utf8Pks = new byte[pks.Length][];
            nint[] ptrs = new nint[pks.Length];
            var handles = new GCHandle[pks.Length];
            
            try
            {
                for (int i = 0; i < pks.Length; i++)
                {
                    // CRITICAL: We MUST append \0 before GetBytes because the native C API expects null-terminated const char*.
                    utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                    handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                    ptrs[i] = handles[i].AddrOfPinnedObject();
                }

                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_delete(
                        _handle, (nint)p, (nuint)pks.Length, out nuint success, out nuint error);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Delete));
                    return new ZVecStatus { Code = (ZVecErrorCode)rc };
                }
            }
            finally
            {
                foreach (var h in handles) if (h.IsAllocated) h.Free();
            }
        }
    }

    public IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks)
    {
        ThrowIfDisposed();
        if (pks.IsEmpty) return [];

        unsafe
        {
            var utf8Pks = new byte[pks.Length][];
            nint[] ptrs = new nint[pks.Length];
            var handles = new GCHandle[pks.Length];
            
            try
            {
                for (int i = 0; i < pks.Length; i++)
                {
                    // CRITICAL: We MUST append \0 before GetBytes because the native C API expects null-terminated const char*.
                    utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                    handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                    ptrs[i] = handles[i].AddrOfPinnedObject();
                }

                fixed (nint* p = ptrs)
                {
                    var rc = NativeMethods.zvec_collection_delete_with_results(
                        _handle, (nint)p, (nuint)pks.Length, out nint resultsPtr, out nuint resCount);
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DeleteWithResults));
                    return UnmarshalWriteResults(resultsPtr, resCount);
                }
            }
            finally
            {
                foreach (var h in handles) if (h.IsAllocated) h.Free();
            }
        }
    }

    public ZVecStatus DeleteByFilter(string filter)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        var rc = NativeMethods.zvec_collection_delete_by_filter(_handle, filter);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DeleteByFilter));
        return new ZVecStatus { Code = (ZVecErrorCode)rc };
    }

    public ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Delete(pk));
    }

    public ValueTask<ZVecStatus> DeleteAsync(IReadOnlyList<string> pks, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pks);
        if (pks is string[] arr) return ValueTask.FromResult(Delete(arr));
        return ValueTask.FromResult(Delete(pks.ToArray()));
    }

    public ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DeleteByFilter(filter));
    }

    public ZVecDoc? Fetch(string pk, bool includeVector = false)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);

        var list = Fetch([pk], includeVector);
        return list.Count > 0 ? list[0] : null;
    }

    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false)
    {
        ThrowIfDisposed();
        if (pks.IsEmpty) return [];

        EnterRead();
        try
        {
            unsafe
            {
                var utf8Pks = new byte[pks.Length][];
                nint[] ptrs = new nint[pks.Length];
                var handles = new GCHandle[pks.Length];

                try
                {
                    for (int i = 0; i < pks.Length; i++)
                    {
                        // CRITICAL: We MUST append \0 before GetBytes because the native C API expects null-terminated const char*.
                        utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                        handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                        ptrs[i] = handles[i].AddrOfPinnedObject();
                    }

                    fixed (nint* p = ptrs)
                    {
                        var rc = NativeMethods.zvec_collection_fetch(
                            _handle, (nint)p, (nuint)pks.Length, IntPtr.Zero, 0, includeVector, out nint docsPtr, out nuint docsCount);
                        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Fetch));
                        return UnmarshalDocs(docsPtr, docsCount);
                    }
                }
                finally
                {
                    foreach (var h in handles) if (h.IsAllocated) h.Free();
                }
            }
        }
        finally
        {
            ExitRead();
        }
    }

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Fetch(pk, includeVector));
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(IReadOnlyList<string> pks, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(pks);
        if (pks is string[] arr) return ValueTask.FromResult(Fetch(arr, includeVector));
        return ValueTask.FromResult(Fetch(pks.ToArray(), includeVector));
    }

    private unsafe IReadOnlyList<ZVecWriteResult> UnmarshalWriteResults(nint resultsPtr, nuint count)
    {
        if (resultsPtr == IntPtr.Zero || count == 0) return [];
        
        var list = new List<ZVecWriteResult>((int)count);
        try
        {
            // CRITICAL: resultsPtr is an array of structs (zvec_write_result_t*), NOT an array of pointers (zvec_write_result_t**).
            // It must be read by calculating the offset of each struct using its size, rather than copying pointers.
            // Treating it as an array of pointers will dereference garbage memory and cause catastrophic access violations.
            int structSize = Marshal.SizeOf<ZVecWriteResultNative>();
            for (int i = 0; i < (int)count; i++)
            {
                var ptr = IntPtr.Add(resultsPtr, i * structSize);
                var nativeRes = Marshal.PtrToStructure<ZVecWriteResultNative>(ptr);
                list.Add(new ZVecWriteResult
                {
                    Code = (ZVecErrorCode)nativeRes.Code,
                    Message = nativeRes.Message != IntPtr.Zero ? Marshal.PtrToStringUTF8(nativeRes.Message) : null
                });
            }
        }
        finally
        {
            NativeMethods.zvec_write_results_free(resultsPtr, count);
        }
        
        return list;
    }

    private unsafe IReadOnlyList<ZVecDoc> UnmarshalDocs(nint docsPtr, nuint count)
    {
        if (docsPtr == IntPtr.Zero || count == 0) return [];

        var list = new List<ZVecDoc>((int)count);
        try
        {
            var ptrArray = new nint[count];
            Marshal.Copy(docsPtr, ptrArray, 0, (int)count);

            for (int i = 0; i < (int)count; i++)
            {
                var doc = NativeDocUnmarshaller.Unmarshal(ptrArray[i], Schema);
                list.Add(doc);
            }
        }
        finally
        {
            NativeMethods.zvec_docs_free(docsPtr, count);
        }
        
        return list;
    }

    // =========================================================================
    // Epic E13 — Query
    // =========================================================================

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);

        query = PrepareQuery(query);
        if (RequiresMultiQuery(query))
            return Query([query], topk, filter: filter);

        EnterRead();
        try
        {
            using var builder = new NativeQueryBuilder(query, topk, filter);

            int rc = NativeMethods.zvec_collection_query(
                _handle,
                builder.Handle,
                out nint resultsPtr,
                out nuint resultCount);

            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Query));

            return UnmarshalDocs(resultsPtr, resultCount);
        }
        finally
        {
            ExitRead();
        }
    }

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk, ZVecFilterBuilder filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Query(query, topk, filter.Build());
    }

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null)
    {
        ThrowIfDisposed();
        if (queries is null || queries.Count == 0) return [];

        var prepared = new ZVecQuery[queries.Count];
        for (int i = 0; i < queries.Count; i++)
            prepared[i] = PrepareQuery(queries[i]);

        EnterRead();
        try
        {
            using var builder = new Internal.NativeMultiQueryBuilder(prepared, topk, reranker, filter);
            int rc = NativeMethods.zvec_collection_multi_query(
                _handle,
                builder.Handle,
                out nint resultsPtr,
                out nuint resultCount);

            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Query));

            return UnmarshalDocs(resultsPtr, resultCount);
        }
        finally
        {
            ExitRead();
        }
    }

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        ZVecFilterBuilder filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Query(queries, topk, reranker, filter.Build());
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk = 10, string? filter = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk, ZVecFilterBuilder filter, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        ZVecReranker? reranker = null,
        string? filter = null,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(queries, topk, reranker, filter));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(
        IReadOnlyList<ZVecQuery> queries,
        int topk,
        ZVecReranker? reranker,
        ZVecFilterBuilder filter,
        CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(queries, topk, reranker, filter));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    public IReadOnlyList<ZVecDoc> QueryGroupBy(ZVecGroupByQuery groupQuery)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(groupQuery);
        throw new NotSupportedException(ZVecDefaults.Errors.NativeGroupByQueryNotSupported);
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryGroupByAsync(ZVecGroupByQuery groupQuery, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled<IReadOnlyList<ZVecDoc>>(ct);
        try
        {
            return new ValueTask<IReadOnlyList<ZVecDoc>>(QueryGroupBy(groupQuery));
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<IReadOnlyList<ZVecDoc>>(ex);
        }
    }

    private static bool RequiresMultiQuery(ZVecQuery query) =>
        query.SparseVector is { Count: > 0 };

    private ZVecQuery PrepareQuery(ZVecQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.DocumentId))
            return query;

        if (query.Vector.HasValue || query.SparseVector is { Count: > 0 } || query.Fts != null)
            throw new ArgumentException(ZVecDefaults.Errors.QueryDocumentIdConflict, nameof(query));

        var doc = Fetch(query.DocumentId, includeVector: true);
        if (doc is null)
            throw new KeyNotFoundException(string.Format(ZVecDefaults.Errors.QueryDocumentNotFound, query.DocumentId));

        if (doc.DenseVectors.TryGetValue(query.FieldName, out var dense))
        {
            return new ZVecQuery
            {
                FieldName = query.FieldName,
                Vector = dense,
                Fts = query.Fts,
                QueryParams = query.QueryParams
            };
        }

        if (doc.SparseVectors.TryGetValue(query.FieldName, out var sparse))
        {
            return new ZVecQuery
            {
                FieldName = query.FieldName,
                SparseVector = sparse,
                Fts = query.Fts,
                QueryParams = query.QueryParams
            };
        }

        throw new ArgumentException(
            string.Format(ZVecDefaults.Errors.QueryFieldNotOnDocument, query.FieldName, query.DocumentId),
            nameof(query));
    }

    // =========================================================================
    // Epic E14 — DDL
    // =========================================================================

    public void AddColumn(ZVecFieldSchema field, string? defaultExpression = null)
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            using var builder = new NativeFieldSchemaBuilder(field);
            int rc = NativeMethods.zvec_collection_add_column(_handle, builder.Handle, defaultExpression ?? string.Empty);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddColumn));
        }
        finally
        {
            ExitNativeCall();
        }

        if (Schema != null)
        {
            var fields = Schema.Fields.ToList();
            fields.Add(field);
            Schema = new ZVecCollectionSchema { Name = Schema.Name, Fields = fields, Vectors = Schema.Vectors };
        }
    }

    public void DropColumn(string columnName)
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_drop_column(_handle, columnName);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropColumn));
        }
        finally
        {
            ExitNativeCall();
        }

        if (Schema != null)
        {
            var fields = Schema.Fields.Where(f => f.Name != columnName).ToList();
            var vectors = Schema.Vectors.Where(v => v.Name != columnName).ToList();
            Schema = new ZVecCollectionSchema { Name = Schema.Name, Fields = fields, Vectors = vectors };
        }
    }

    public void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null)
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            using var builder = newSchema != null ? new NativeFieldSchemaBuilder(newSchema) : null;
            int rc = NativeMethods.zvec_collection_alter_column(
                _handle,
                columnName,
                newName,
                builder?.Handle ?? IntPtr.Zero);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AlterColumn));
        }
        finally
        {
            ExitNativeCall();
        }

        if (Schema != null)
        {
            var fields = Schema.Fields.ToList();
            var target = fields.FirstOrDefault(f => f.Name == columnName);
            if (target != null)
            {
                var replacement = newSchema ?? target;
                var finalField = new ZVecFieldSchema
                {
                    Name = newName ?? replacement.Name,
                    DataType = replacement.DataType,
                    Nullable = replacement.Nullable
                };
                fields[fields.IndexOf(target)] = finalField;
                Schema = new ZVecCollectionSchema { Name = Schema.Name, Fields = fields, Vectors = Schema.Vectors };
            }
        }
    }

    public void CreateIndex(string columnName, ZVecIndexParam indexParam)
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            using var builder = new NativeIndexParamBuilder(indexParam);
            int rc = NativeMethods.zvec_collection_create_index(_handle, columnName, builder.Handle);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(CreateIndex));
        }
        finally
        {
            ExitNativeCall();
        }
    }

    public void DropIndex(string columnName)
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_drop_index(_handle, columnName);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropIndex));
        }
        finally
        {
            ExitNativeCall();
        }
    }

    public void Optimize()
    {
        ThrowIfDisposed();
        EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_optimize(_handle);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Optimize));
        }
        finally
        {
            ExitNativeCall();
        }
    }
    
    public ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            AddColumn(field, defaultExpression);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask DropColumnAsync(string columnName, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            DropColumn(columnName);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask AlterColumnAsync(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            AlterColumn(columnName, newName, newSchema);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            CreateIndex(columnName, indexParam);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask DropIndexAsync(string columnName, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            DropIndex(columnName);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    public ValueTask OptimizeAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        try
        {
            Optimize();
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }
}

