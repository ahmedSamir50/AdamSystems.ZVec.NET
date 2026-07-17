using System.Runtime.InteropServices;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>Insert / update / upsert / delete native write paths.</summary>
internal sealed class CollectionWriteOps
{
    private enum DocWriteKind
    {
        Insert,
        Update,
        Upsert,
        InsertWithResults,
        UpdateWithResults,
        UpsertWithResults
    }

    private readonly CollectionNativeContext _ctx;

    public CollectionWriteOps(CollectionNativeContext ctx) => _ctx = ctx;

    public ZVecStatus Insert(ZVecDoc doc)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);
        return Insert([doc]);
    }

    public ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };
        return ExecuteDocWrite(docs, DocWriteKind.Insert);
    }

    public IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return [];
        return ExecuteDocWriteWithResults(docs, DocWriteKind.InsertWithResults);
    }

    public ZVecStatus Update(ZVecDoc doc)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);
        return Update([doc]);
    }

    public ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };
        return ExecuteDocWrite(docs, DocWriteKind.Update);
    }

    public IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return [];
        return ExecuteDocWriteWithResults(docs, DocWriteKind.UpdateWithResults);
    }

    public ZVecStatus Upsert(ZVecDoc doc)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(doc);
        return Upsert([doc]);
    }

    public ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };
        return ExecuteDocWrite(docs, DocWriteKind.Upsert);
    }

    public IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        _ctx.ThrowIfDisposed();
        if (docs.IsEmpty) return [];
        return ExecuteDocWriteWithResults(docs, DocWriteKind.UpsertWithResults);
    }

    public ZVecStatus Delete(string pk)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(pk);
        return Delete([pk]);
    }

    public ZVecStatus Delete(ReadOnlySpan<string> pks)
    {
        _ctx.ThrowIfDisposed();
        if (pks.IsEmpty) return new ZVecStatus { Code = ZVecErrorCode.Ok };

        _ctx.Gate.EnterNativeCall();
        try
        {
            return DeleteCore(pks);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks)
    {
        _ctx.ThrowIfDisposed();
        if (pks.IsEmpty) return [];

        _ctx.Gate.EnterNativeCall();
        try
        {
            return DeleteWithResultsCore(pks);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public ZVecStatus DeleteByFilter(string filter)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);

        _ctx.Gate.EnterNativeCall();
        try
        {
            var rc = NativeMethods.zvec_collection_delete_by_filter(_ctx.Handle, filter);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DeleteByFilter));
            return new ZVecStatus { Code = (ZVecErrorCode)rc };
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    private ZVecStatus ExecuteDocWrite(ReadOnlySpan<ZVecDoc> docs, DocWriteKind kind)
    {
        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = System.Buffers.ArrayPool<nint>.Shared.Rent(docs.Length);
        _ctx.Gate.EnterNativeCall();
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
                    int rc = InvokeDocWrite(kind, _ctx.DangerousHandle, (nint)p, (nuint)docs.Length, out _, out _);
                    string opName = kind switch
                    {
                        DocWriteKind.Insert => nameof(Insert),
                        DocWriteKind.Update => nameof(Update),
                        _ => nameof(Upsert)
                    };
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, opName);
                    return new ZVecStatus { Code = (ZVecErrorCode)rc };
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            System.Buffers.ArrayPool<nint>.Shared.Return(ptrs, clearArray: true);
            _ctx.Gate.ExitNativeCall();
        }
    }

    private IReadOnlyList<ZVecWriteResult> ExecuteDocWriteWithResults(ReadOnlySpan<ZVecDoc> docs, DocWriteKind kind)
    {
        var builders = new NativeDocBuilder[docs.Length];
        nint[] ptrs = System.Buffers.ArrayPool<nint>.Shared.Rent(docs.Length);
        _ctx.Gate.EnterNativeCall();
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
                    int rc = InvokeDocWriteWithResults(kind, _ctx.DangerousHandle, (nint)p, (nuint)docs.Length, out nint resultsPtr, out nuint resCount);
                    string opName = kind switch
                    {
                        DocWriteKind.InsertWithResults => nameof(InsertWithResults),
                        DocWriteKind.UpdateWithResults => nameof(UpdateWithResults),
                        _ => nameof(UpsertWithResults)
                    };
                    ZVecError.ThrowIfFailed((ZVecErrorCode)rc, opName);
                    return _ctx.UnmarshalWriteResults(resultsPtr, resCount);
                }
            }
        }
        finally
        {
            foreach (var b in builders) b?.Dispose();
            System.Buffers.ArrayPool<nint>.Shared.Return(ptrs, clearArray: true);
            _ctx.Gate.ExitNativeCall();
        }
    }

    private static int InvokeDocWrite(DocWriteKind kind, nint handle, nint ptrs, nuint len, out nuint success, out nuint error) =>
        kind switch
        {
            DocWriteKind.Insert => NativeMethods.zvec_collection_insert(handle, ptrs, len, out success, out error),
            DocWriteKind.Update => NativeMethods.zvec_collection_update(handle, ptrs, len, out success, out error),
            DocWriteKind.Upsert => NativeMethods.zvec_collection_upsert(handle, ptrs, len, out success, out error),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static int InvokeDocWriteWithResults(
        DocWriteKind kind, nint handle, nint ptrs, nuint len, out nint resultsPtr, out nuint resCount) =>
        kind switch
        {
            DocWriteKind.InsertWithResults =>
                NativeMethods.zvec_collection_insert_with_results(handle, ptrs, len, out resultsPtr, out resCount),
            DocWriteKind.UpdateWithResults =>
                NativeMethods.zvec_collection_update_with_results(handle, ptrs, len, out resultsPtr, out resCount),
            DocWriteKind.UpsertWithResults =>
                NativeMethods.zvec_collection_upsert_with_results(handle, ptrs, len, out resultsPtr, out resCount),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private unsafe ZVecStatus DeleteCore(ReadOnlySpan<string> pks)
    {
        var utf8Pks = new byte[pks.Length][];
        nint[] ptrs = new nint[pks.Length];
        var handles = new GCHandle[pks.Length];

        try
        {
            for (int i = 0; i < pks.Length; i++)
            {
                utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                ptrs[i] = handles[i].AddrOfPinnedObject();
            }

            fixed (nint* p = ptrs)
            {
                var rc = NativeMethods.zvec_collection_delete(
                    _ctx.Handle, (nint)p, (nuint)pks.Length, out nuint success, out nuint error);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Delete));
                return new ZVecStatus { Code = (ZVecErrorCode)rc };
            }
        }
        finally
        {
            foreach (var h in handles) if (h.IsAllocated) h.Free();
        }
    }

    private unsafe IReadOnlyList<ZVecWriteResult> DeleteWithResultsCore(ReadOnlySpan<string> pks)
    {
        var utf8Pks = new byte[pks.Length][];
        nint[] ptrs = new nint[pks.Length];
        var handles = new GCHandle[pks.Length];

        try
        {
            for (int i = 0; i < pks.Length; i++)
            {
                utf8Pks[i] = System.Text.Encoding.UTF8.GetBytes(pks[i] + "\0");
                handles[i] = GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
                ptrs[i] = handles[i].AddrOfPinnedObject();
            }

            fixed (nint* p = ptrs)
            {
                var rc = NativeMethods.zvec_collection_delete_with_results(
                    _ctx.Handle, (nint)p, (nuint)pks.Length, out nint resultsPtr, out nuint resCount);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DeleteWithResults));
                return _ctx.UnmarshalWriteResults(resultsPtr, resCount);
            }
        }
        finally
        {
            foreach (var h in handles) if (h.IsAllocated) h.Free();
        }
    }
}
