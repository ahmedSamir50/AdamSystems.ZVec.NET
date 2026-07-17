using System.Runtime.InteropServices;
using ZVec.NET.Concurrency;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>
/// Shared native handle, schema cache, and call gates for collection operation helpers.
/// </summary>
internal sealed class CollectionNativeContext
{
    private readonly SafeZvecHandle _safeHandle;
    private readonly nint _cachedHandle;
    private readonly ZVecFactory _factory;
    private readonly nint _handleKey;
    private int _disposed;
    private int _destroyed;
    private ZVecCollectionSchema? _schema;
    private Dictionary<string, ZVecDataType>? _fieldTypeMap;

    public CollectionNativeContext(
        nint handle,
        ZVecFactory factory,
        CancellationToken factoryShutdownToken,
        ZVecCollectionOptions options,
        ZVecCollectionSchema? schema)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle, nameof(handle));
        _handleKey = handle;
        _safeHandle = new SafeZvecHandle(handle, ownsHandle: true);
        _cachedHandle = _safeHandle.DangerousGetHandle();
        _factory = factory;
        Options = options;
        Schema = schema;

        SemaphoreSlim? readGate = null;
        int maxReads = options.MaxConcurrentReads;
        if (maxReads > 0)
            readGate = new SemaphoreSlim(maxReads, maxReads);

        Gate = new CollectionCallGate(factory, factoryShutdownToken, readGate);
    }

    public CollectionCallGate Gate { get; }

    public ZVecCollectionOptions Options { get; }

    public ZVecCollectionSchema? Schema
    {
        get => _schema;
        set
        {
            _schema = value;
            _fieldTypeMap = BuildFieldTypeMap(value);
        }
    }

    /// <summary>Name → data-type map rebuilt whenever <see cref="Schema"/> changes.</summary>
    public IReadOnlyDictionary<string, ZVecDataType>? FieldTypeMap => _fieldTypeMap;

    public nint HandleKey => _handleKey;

    /// <summary>
    /// Cached native pointer (stable until Dispose). Callers must <see cref="ThrowIfDisposed"/> once per op.
    /// </summary>
    public nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _cachedHandle;
        }
    }

    /// <summary>Handle without disposed check — only after an outer <see cref="ThrowIfDisposed"/>.</summary>
    public nint DangerousHandle => _cachedHandle;

    public bool IsDisposed => Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;

    public void ThrowIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(ZVecCollection));
    }

    /// <summary>Closes the collection (idempotent). Does not delete on-disk data.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _factory.OpenCollections.TryRemove(_handleKey, out _);
        _safeHandle.Dispose();
        Gate.DisposeReadGate();
    }

    /// <summary>
    /// Destroys on-disk data then closes. Throws if the collection was already closed via <see cref="Dispose"/>.
    /// Idempotent after a successful destroy.
    /// </summary>
    public void Destroy()
    {
        if (Interlocked.Exchange(ref _destroyed, 1) != 0)
            return;

        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            throw new ObjectDisposedException(nameof(ZVecCollection), ZVecDefaults.Errors.DestroyAfterDispose);

        try
        {
            if (ZVecFactory.IsNativeLibraryInitialized && !_safeHandle.IsInvalid)
            {
                _ = NativeMethods.zvec_collection_destroy(_cachedHandle);
            }
        }
        finally
        {
            _factory.OpenCollections.TryRemove(_handleKey, out _);
            _safeHandle.Dispose();
            Gate.DisposeReadGate();
        }
    }

    public unsafe IReadOnlyList<ZVecWriteResult> UnmarshalWriteResults(nint resultsPtr, nuint count)
    {
        if (resultsPtr == IntPtr.Zero || count == 0) return [];

        var list = new List<ZVecWriteResult>((int)count);
        try
        {
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

    public unsafe IReadOnlyList<ZVecDoc> UnmarshalDocs(nint docsPtr, nuint count, bool includeVector = true)
    {
        if (docsPtr == IntPtr.Zero || count == 0) return [];

        var list = new List<ZVecDoc>((int)count);
        try
        {
            nint* ptrs = (nint*)docsPtr;
            for (int i = 0; i < (int)count; i++)
            {
                list.Add(NativeDocUnmarshaller.Unmarshal(ptrs[i], _fieldTypeMap, includeVector));
            }
        }
        finally
        {
            NativeMethods.zvec_docs_free(docsPtr, count);
        }

        return list;
    }

    private static Dictionary<string, ZVecDataType>? BuildFieldTypeMap(ZVecCollectionSchema? schema)
    {
        if (schema is null)
            return null;

        int n = schema.Fields.Count + schema.Vectors.Count;
        var map = new Dictionary<string, ZVecDataType>(n, StringComparer.Ordinal);
        foreach (var f in schema.Fields)
            map[f.Name] = f.DataType;
        foreach (var v in schema.Vectors)
            map[v.Name] = v.DataType;
        return map;
    }
}
