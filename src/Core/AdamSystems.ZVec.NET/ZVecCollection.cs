using AdamSystems.ZVec.NET.Internal;
using AdamSystems.ZVec.NET.Interop;
using System.Runtime.InteropServices;

namespace AdamSystems.ZVec.NET;

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
/// This mirrors the BCL <see cref="System.Runtime.InteropServices.SafeHandle"/> pattern.
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
    public ZVecCollectionSchema? Schema { get; }

    // Token cancelled when ZVecFactory.Shutdown() is called.
    private readonly CancellationToken _factoryShutdownToken;

    internal ZVecCollection(nint handle, string path, ZVecCollectionSchema? schema, CancellationToken factoryShutdownToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle, nameof(handle));
        _handle = handle;
        Path = path;
        Schema = schema;
        _factoryShutdownToken = factoryShutdownToken;
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
        _ = NativeMethods.zvec_collection_close(_handle);
        ZVecFactory.OpenCollections.TryRemove(_handle, out _);
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

        // Destroy deletes the on-disk data. Capture result but don't throw during cleanup.
        _ = NativeMethods.zvec_collection_destroy(_handle);

        // Close the handle (no-op if Dispose() already ran concurrently,
        // but _disposed guards against double-close).
        Dispose();
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
    // Epic E12 — CRUD
    // =========================================================================

    public ZVecStatus Insert(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);

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
        if (docs.IsEmpty) return Array.Empty<ZVecWriteResult>();

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
        if (docs is null) throw new ArgumentNullException(nameof(docs));
        if (docs is ZVecDoc[] arr) return ValueTask.FromResult(InsertWithResults(arr));
        return ValueTask.FromResult(InsertWithResults(docs.ToArray()));
    }

    public ZVecStatus Update(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);

        using var builder = NativeDocBuilder.Build(doc);
        nint handle = builder.Handle;
        unsafe
        {
            nint* ptr = &handle;
            var rc = NativeMethods.zvec_collection_update(
                _handle, (nint)ptr, 1, out nuint success, out nuint error);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Update));
            return new ZVecStatus { Code = (ZVecErrorCode)rc };
        }
    }

    public ZVecStatus Upsert(ZVecDoc doc)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);

        using var builder = NativeDocBuilder.Build(doc);
        nint handle = builder.Handle;
        unsafe
        {
            nint* ptr = &handle;
            var rc = NativeMethods.zvec_collection_upsert(
                _handle, (nint)ptr, 1, out nuint success, out nuint error);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Upsert));
            return new ZVecStatus { Code = (ZVecErrorCode)rc };
        }
    }

    public ZVecStatus Delete(string pk)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);

        unsafe
        {
            var utf8Pk = System.Text.Encoding.UTF8.GetBytes(pk);
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
            var handles = new System.Runtime.InteropServices.GCHandle[pks.Length];
            
            try
            {
                for (int i = 0; i < pks.Length; i++)
                {
                    utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i]);
                    handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(utf8Pks[i], System.Runtime.InteropServices.GCHandleType.Pinned);
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
        if (pks.IsEmpty) return Array.Empty<ZVecWriteResult>();

        unsafe
        {
            var utf8Pks = new byte[pks.Length][];
            nint[] ptrs = new nint[pks.Length];
            var handles = new System.Runtime.InteropServices.GCHandle[pks.Length];
            
            try
            {
                for (int i = 0; i < pks.Length; i++)
                {
                    utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i]);
                    handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(utf8Pks[i], System.Runtime.InteropServices.GCHandleType.Pinned);
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

    public ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(DeleteByFilter(filter));
    }

    public ZVecDoc? Fetch(string pk, bool includeVector = false)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);

        var list = Fetch(new[] { pk }, includeVector);
        return list.Count > 0 ? list[0] : null;
    }

    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false)
    {
        ThrowIfDisposed();
        if (pks.IsEmpty) return Array.Empty<ZVecDoc>();

        unsafe
        {
            var utf8Pks = new byte[pks.Length][];
            nint[] ptrs = new nint[pks.Length];
            var handles = new System.Runtime.InteropServices.GCHandle[pks.Length];
            
            try
            {
                for (int i = 0; i < pks.Length; i++)
                {
                    utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i]);
                    handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(utf8Pks[i], System.Runtime.InteropServices.GCHandleType.Pinned);
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

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Fetch(pk, includeVector));
    }

    private unsafe IReadOnlyList<ZVecWriteResult> UnmarshalWriteResults(nint resultsPtr, nuint count)
    {
        if (resultsPtr == IntPtr.Zero || count == 0) return Array.Empty<ZVecWriteResult>();
        
        var list = new List<ZVecWriteResult>((int)count);
        try
        {
            var ptrArray = new nint[count];
            Marshal.Copy(resultsPtr, ptrArray, 0, (int)count);
            for (int i = 0; i < (int)count; i++)
            {
                var nativeRes = Marshal.PtrToStructure<ZVecWriteResultNative>(ptrArray[i]);
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
        if (docsPtr == IntPtr.Zero || count == 0) return Array.Empty<ZVecDoc>();

        var list = new List<ZVecDoc>((int)count);
        try
        {
            var ptrArray = new nint[count];
            Marshal.Copy(docsPtr, ptrArray, 0, (int)count);

            for (int i = 0; i < (int)count; i++)
            {
                var doc = Internal.NativeDocUnmarshaller.Unmarshal(ptrArray[i], Schema);
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
    // Epic E13 — Query (Stubs)
    // =========================================================================

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(query);

        using var builder = new Internal.NativeQueryBuilder(query, topk, filter);
        
        int rc = NativeMethods.zvec_collection_query(
            _handle, 
            builder.Handle, 
            out nint resultsPtr, 
            out nuint resultCount);

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Query));

        return UnmarshalDocs(resultsPtr, resultCount);
    }

    public IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null) 
        => throw new NotImplementedException("Multi-query requires zvec_multi_query implementation.");

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

    // =========================================================================
    // Epic E14 — DDL (Stubs)
    // =========================================================================

    public void AddColumn(ZVecFieldSchema field, string? defaultExpression = null)
    {
        ThrowIfDisposed();
        using var builder = new Internal.NativeFieldSchemaBuilder(field);
        int rc = NativeMethods.zvec_collection_add_column(_handle, builder.Handle, defaultExpression);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddColumn));
    }

    public void DropColumn(string columnName)
    {
        ThrowIfDisposed();
        int rc = NativeMethods.zvec_collection_drop_column(_handle, columnName);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropColumn));
    }

    public void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null)
    {
        ThrowIfDisposed();
        using var builder = newSchema != null ? new Internal.NativeFieldSchemaBuilder(newSchema) : null;
        int rc = NativeMethods.zvec_collection_alter_column(
            _handle, 
            columnName, 
            newName, 
            builder?.Handle ?? IntPtr.Zero);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AlterColumn));
    }

    public void CreateIndex(string columnName, ZVecIndexParam indexParam)
    {
        ThrowIfDisposed();
        using var builder = new Internal.NativeIndexParamBuilder(indexParam);
        int rc = NativeMethods.zvec_collection_create_index(_handle, columnName, builder.Handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(CreateIndex));
    }

    public void DropIndex(string columnName)
    {
        ThrowIfDisposed();
        int rc = NativeMethods.zvec_collection_drop_index(_handle, columnName);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropIndex));
    }

    public void Optimize()
    {
        ThrowIfDisposed();
        int rc = NativeMethods.zvec_collection_optimize(_handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Optimize));
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

