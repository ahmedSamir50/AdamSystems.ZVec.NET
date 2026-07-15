using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal sealed class NativeFieldSchemaBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;
    private NativeIndexParamBuilder? _indexParamBuilder;

    public nint Handle => _handle;

    public NativeFieldSchemaBuilder(ZVecFieldSchema field)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));

        _handle = NativeMethods.zvec_field_schema_create(field.Name, (int)field.DataType, field.Nullable, 0);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeFieldSchemaCreateFailed);

        try
        {
            if (field.IndexParam != null)
            {
                _indexParamBuilder = new NativeIndexParamBuilder(field.IndexParam);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_field_schema_set_index_params(_handle, _indexParamBuilder.Handle), 
                    nameof(NativeMethods.zvec_field_schema_set_index_params));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public NativeFieldSchemaBuilder(ZVecVectorSchema vector)
    {
        if (vector == null)
            throw new ArgumentNullException(nameof(vector));

        _handle = NativeMethods.zvec_field_schema_create(vector.Name, (int)vector.DataType, false, (uint)vector.Dimension);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeFieldSchemaCreateFailed);

        try
        {
            if (vector.IndexParam != null)
            {
                _indexParamBuilder = new NativeIndexParamBuilder(vector.IndexParam);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_field_schema_set_index_params(_handle, _indexParamBuilder.Handle), 
                    nameof(NativeMethods.zvec_field_schema_set_index_params));
            }
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
            NativeMethods.zvec_field_schema_destroy(_handle);
        }

        _indexParamBuilder?.Dispose();
    }
}
