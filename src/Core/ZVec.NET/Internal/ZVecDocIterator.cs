using System.Collections;
using System.Runtime.InteropServices;
using ZVec.NET.Internal;
using ZVec.NET.Interop;

namespace ZVec.NET;

/// <summary>
/// Snapshot iterator over all documents in a collection. Holds the collection read gate until disposed.
/// </summary>
public sealed class ZVecDocIterator : IEnumerable<ZVecDoc>, IEnumerator<ZVecDoc>, IDisposable
{
    private readonly CollectionNativeContext _ctx;
    private readonly bool _includeVector;
    private nint _iterHandle;
    private bool _disposed;
    private bool _gateHeld;
    private ZVecDoc? _current;

    private ZVecDocIterator(CollectionNativeContext ctx, nint iterHandle, bool includeVector, bool gateHeld)
    {
        _ctx = ctx;
        _iterHandle = iterHandle;
        _includeVector = includeVector;
        _gateHeld = gateHeld;
    }

    internal static unsafe ZVecDocIterator Create(
        CollectionNativeContext ctx,
        nint collectionHandle,
        ZVecIterateOptions options,
        bool gateHeld)
    {
        ArgumentNullException.ThrowIfNull(options);

        nint optionsHandle = IntPtr.Zero;
        nint[]? fieldPtrs = null;
        GCHandle[]? fieldHandles = null;

        try
        {
            if (options.OutputFields is { Count: > 0 })
            {
                optionsHandle = NativeMethods.zvec_iterator_options_create();
                if (optionsHandle == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeIteratorOptionsCreateFailed);

                fieldPtrs = new nint[options.OutputFields.Count];
                fieldHandles = new GCHandle[options.OutputFields.Count];
                for (int i = 0; i < options.OutputFields.Count; i++)
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(options.OutputFields[i] + "\0");
                    fieldHandles[i] = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    fieldPtrs[i] = fieldHandles[i].AddrOfPinnedObject();
                }

                fixed (nint* p = fieldPtrs)
                {
                    ZVecError.ThrowIfFailed(
                        (ZVecErrorCode)NativeMethods.zvec_iterator_options_set_output_fields(
                            optionsHandle, (nint)p, (nuint)options.OutputFields.Count),
                        nameof(NativeMethods.zvec_iterator_options_set_output_fields));
                }
            }
            else if (options.OutputFields is { Count: 0 })
            {
                optionsHandle = NativeMethods.zvec_iterator_options_create();
                if (optionsHandle == IntPtr.Zero)
                    throw new InvalidOperationException(ZVecDefaults.Errors.NativeIteratorOptionsCreateFailed);

                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_iterator_options_set_output_fields(
                        optionsHandle, IntPtr.Zero, 0),
                    nameof(NativeMethods.zvec_iterator_options_set_output_fields));
            }

            if (optionsHandle != IntPtr.Zero)
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_iterator_options_set_include_vector(
                        optionsHandle, options.IncludeVector),
                    nameof(NativeMethods.zvec_iterator_options_set_include_vector));
            }

            nint iterHandle = IntPtr.Zero;
            var rc = NativeMethods.zvec_collection_create_iterator(
                collectionHandle,
                optionsHandle,
                out iterHandle);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_create_iterator));
            if (iterHandle == IntPtr.Zero)
                throw new InvalidOperationException(ZVecDefaults.Errors.NativeIteratorCreateFailed);

            return new ZVecDocIterator(ctx, iterHandle, options.IncludeVector, gateHeld);
        }
        finally
        {
            if (optionsHandle != IntPtr.Zero)
                NativeMethods.zvec_iterator_options_destroy(optionsHandle);

            if (fieldHandles is not null)
            {
                foreach (var h in fieldHandles)
                {
                    if (h.IsAllocated)
                        h.Free();
                }
            }
        }
    }

    /// <summary>Factory when the read gate is already held by the caller.</summary>
    internal static ZVecDocIterator CreateWithGateHeld(
        CollectionNativeContext ctx,
        nint collectionHandle,
        ZVecIterateOptions options)
        => Create(ctx, collectionHandle, options, gateHeld: true);

    public ZVecDoc Current => _current ?? throw new InvalidOperationException("Iterator is before first or after last element.");

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        nint docPtr = IntPtr.Zero;
        var rc = NativeMethods.zvec_doc_iterator_next(_iterHandle, out docPtr);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_doc_iterator_next));

        if (docPtr == IntPtr.Zero)
        {
            _current = null;
            return false;
        }

        try
        {
            _current = NativeDocUnmarshaller.Unmarshal(docPtr, _ctx.FieldTypeMap, _includeVector);
        }
        finally
        {
            NativeMethods.zvec_doc_destroy(docPtr);
        }

        return true;
    }

    public void Reset() => throw new NotSupportedException("DocIterator does not support Reset().");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_iterHandle != IntPtr.Zero)
        {
            NativeMethods.zvec_doc_iterator_close(_iterHandle);
            _iterHandle = IntPtr.Zero;
        }

        if (_gateHeld)
        {
            _ctx.Gate.ExitRead();
            _gateHeld = false;
        }
    }

    public IEnumerator<ZVecDoc> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
