using System.Buffers;
using System.Runtime.InteropServices;
using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal sealed unsafe class NativeQueryBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;
    private readonly List<MemoryHandle> _pinnedHandles = [];

    public nint Handle => _handle;

    public NativeQueryBuilder(ZVecQuery query, int topk, string? filter)
    {
        _handle = NativeMethods.zvec_vector_query_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeQueryCreateFailed);

        try
        {
            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_field_name(_handle, query.FieldName), 
                nameof(NativeMethods.zvec_vector_query_set_field_name));
                
            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_topk(_handle, topk), 
                nameof(NativeMethods.zvec_vector_query_set_topk));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_filter(_handle, filter), 
                    nameof(NativeMethods.zvec_vector_query_set_filter));
            }

            if (query.Vector.HasValue)
            {
                var memHandle = query.Vector.Value.Pin();
                _pinnedHandles.Add(memHandle);
                
                nuint size = (nuint)(query.Vector.Value.Length * sizeof(float));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_vector_query_set_query_vector(_handle, new IntPtr(memHandle.Pointer), size), 
                    nameof(NativeMethods.zvec_vector_query_set_query_vector));
            }

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_vector_query_set_include_vector(_handle, true), 
                nameof(NativeMethods.zvec_vector_query_set_include_vector));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.zvec_vector_query_destroy(_handle);
        }

        foreach (var pinned in _pinnedHandles)
        {
            pinned.Dispose();
        }
        _pinnedHandles.Clear();
    }
}
