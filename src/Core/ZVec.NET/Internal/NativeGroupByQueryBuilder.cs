using System.Buffers;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>
/// Builds a <c>zvec_group_by_vector_query_t</c> for parity with upstream builders.
/// Does not execute: Python runs group-by via pybind → <c>Collection::GroupByQuery</c>;
/// <c>c_api.h</c> has no <c>zvec_collection_group_by_query</c>.
/// </summary>
internal sealed unsafe class NativeGroupByQueryBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;
    private readonly List<MemoryHandle> _pinnedHandles = [];

    public nint Handle => _handle;

    public NativeGroupByQueryBuilder(ZVecGroupByQuery groupQuery, bool includeVector = true)
    {
        ArgumentNullException.ThrowIfNull(groupQuery);
        ArgumentNullException.ThrowIfNull(groupQuery.Query);

        _handle = NativeMethods.zvec_group_by_vector_query_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeGroupByQueryCreateFailed);

        try
        {
            ZVecQuery query = groupQuery.Query;

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_field_name(_handle, query.FieldName),
                nameof(NativeMethods.zvec_group_by_vector_query_set_field_name));

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_group_by_field_name(_handle, groupQuery.GroupByField),
                nameof(NativeMethods.zvec_group_by_vector_query_set_group_by_field_name));

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_group_count(_handle, (uint)groupQuery.Topk),
                nameof(NativeMethods.zvec_group_by_vector_query_set_group_count));

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_topk_per_group(_handle, (uint)groupQuery.GroupSize),
                nameof(NativeMethods.zvec_group_by_vector_query_set_topk_per_group));

            string? filter = groupQuery.Filter ?? null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_filter(_handle, filter),
                    nameof(NativeMethods.zvec_group_by_vector_query_set_filter));
            }

            if (query.Vector.HasValue)
            {
                var memHandle = query.Vector.Value.Pin();
                _pinnedHandles.Add(memHandle);
                nuint size = (nuint)(query.Vector.Value.Length * sizeof(float));
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_query_vector(
                        _handle, new IntPtr(memHandle.Pointer), size),
                    nameof(NativeMethods.zvec_group_by_vector_query_set_query_vector));
            }

            NativeQueryParamsApplicator.Apply(
                _handle,
                query.QueryParams,
                NativeQueryParamsApplicator.QueryTarget.GroupByQuery);

            ZVecError.ThrowIfFailed(
                (ZVecErrorCode)NativeMethods.zvec_group_by_vector_query_set_include_vector(_handle, includeVector),
                nameof(NativeMethods.zvec_group_by_vector_query_set_include_vector));
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
            NativeMethods.zvec_group_by_vector_query_destroy(_handle);

        foreach (var pinned in _pinnedHandles)
            pinned.Dispose();
        _pinnedHandles.Clear();
    }
}
