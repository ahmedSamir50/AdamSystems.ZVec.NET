using System.Runtime.InteropServices;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>
/// Marshals a native collection schema pointer (from <c>zvec_collection_get_schema</c>)
/// into a managed <see cref="ZVecCollectionSchema"/> for FieldTypeMap / unmarshalling.
/// </summary>
internal static class NativeSchemaMarshaller
{
    /// <summary>
    /// Loads the on-disk schema for an open collection via <c>zvec_collection_get_schema</c>.
    /// Caller does not own the returned DTO; native schema pointer is destroyed here.
    /// </summary>
    public static unsafe ZVecCollectionSchema FromOpenCollection(nint collectionHandle)
    {
        ArgumentOutOfRangeException.ThrowIfZero(collectionHandle);

        int rc = NativeMethods.zvec_collection_get_schema(collectionHandle, out nint schemaPtr);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_get_schema));
        if (schemaPtr == IntPtr.Zero)
            throw new InvalidOperationException("zvec_collection_get_schema returned a null schema.");

        try
        {
            return FromNativeSchema(schemaPtr);
        }
        finally
        {
            NativeMethods.zvec_collection_schema_destroy(schemaPtr);
        }
    }

    public static unsafe ZVecCollectionSchema FromNativeSchema(nint schemaPtr)
    {
        ArgumentOutOfRangeException.ThrowIfZero(schemaPtr);

        string name = Marshal.PtrToStringUTF8(NativeMethods.zvec_collection_schema_get_name(schemaPtr))
            ?? string.Empty;
        ulong maxDocs = NativeMethods.zvec_collection_schema_get_max_doc_count_per_segment(schemaPtr);

        var fields = ReadForwardFields(schemaPtr);
        var vectors = ReadVectorFields(schemaPtr);

        return new ZVecCollectionSchema
        {
            Name = name,
            Fields = fields,
            Vectors = vectors,
            MaxDocCountPerSegment = maxDocs > int.MaxValue
                ? int.MaxValue
                : (int)maxDocs
        };
    }

    private static unsafe ZVecFieldSchema[] ReadForwardFields(nint schemaPtr)
    {
        int rc = NativeMethods.zvec_collection_schema_get_forward_fields(
            schemaPtr, out nint fieldsPtr, out nuint count);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_schema_get_forward_fields));

        if (fieldsPtr == IntPtr.Zero || count == 0)
            return [];

        try
        {
            var list = new ZVecFieldSchema[(int)count];
            nint* ptrs = (nint*)fieldsPtr;
            for (int i = 0; i < (int)count; i++)
            {
                nint fieldPtr = ptrs[i];
                string fieldName = Marshal.PtrToStringUTF8(NativeMethods.zvec_field_schema_get_name(fieldPtr))
                    ?? string.Empty;
                list[i] = new ZVecFieldSchema
                {
                    Name = fieldName,
                    DataType = (ZVecDataType)NativeMethods.zvec_field_schema_get_data_type(fieldPtr),
                    Nullable = NativeMethods.zvec_field_schema_is_nullable(fieldPtr)
                };
            }

            return list;
        }
        finally
        {
            NativeMethods.zvec_free(fieldsPtr);
        }
    }

    private static unsafe ZVecVectorSchema[] ReadVectorFields(nint schemaPtr)
    {
        int rc = NativeMethods.zvec_collection_schema_get_vector_fields(
            schemaPtr, out nint fieldsPtr, out nuint count);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_schema_get_vector_fields));

        if (fieldsPtr == IntPtr.Zero || count == 0)
            return [];

        try
        {
            var list = new ZVecVectorSchema[(int)count];
            nint* ptrs = (nint*)fieldsPtr;
            for (int i = 0; i < (int)count; i++)
            {
                nint fieldPtr = ptrs[i];
                string fieldName = Marshal.PtrToStringUTF8(NativeMethods.zvec_field_schema_get_name(fieldPtr))
                    ?? string.Empty;
                list[i] = new ZVecVectorSchema
                {
                    Name = fieldName,
                    DataType = (ZVecDataType)NativeMethods.zvec_field_schema_get_data_type(fieldPtr),
                    Dimension = (int)NativeMethods.zvec_field_schema_get_dimension(fieldPtr)
                };
            }

            return list;
        }
        finally
        {
            NativeMethods.zvec_free(fieldsPtr);
        }
    }
}
