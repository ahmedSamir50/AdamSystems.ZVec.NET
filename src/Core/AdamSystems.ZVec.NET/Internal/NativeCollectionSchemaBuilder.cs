using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal sealed class NativeCollectionSchemaBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;

    public nint Handle => _handle;

    public NativeCollectionSchemaBuilder(ZVecCollectionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        _handle = NativeMethods.zvec_collection_schema_create(schema.Name);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeCollectionSchemaCreateFailed);

        try
        {
            foreach (var field in schema.Fields)
            {
                using var fieldBuilder = new NativeFieldSchemaBuilder(field);
                int rc = NativeMethods.zvec_collection_schema_add_field(_handle, fieldBuilder.Handle);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_schema_add_field));
            }

            foreach (var vector in schema.Vectors)
            {
                using var vectorBuilder = new NativeFieldSchemaBuilder(vector);
                int rc = NativeMethods.zvec_collection_schema_add_field(_handle, vectorBuilder.Handle);
                ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_schema_add_field));
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
            NativeMethods.zvec_collection_schema_destroy(_handle);
        }
    }
}
