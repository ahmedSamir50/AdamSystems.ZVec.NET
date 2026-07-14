using System.Runtime.InteropServices;
using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal static class NativeDocUnmarshaller
{
    public static unsafe ZVecDoc Unmarshal(nint docPtr, ZVecCollectionSchema? schema)
    {
        if (docPtr == IntPtr.Zero)
            throw new ArgumentNullException(nameof(docPtr));

        // 1. Get Primary Key
        nint pkPtr = NativeMethods.zvec_doc_get_pk_copy(docPtr);
        string pk = string.Empty;
        if (pkPtr != IntPtr.Zero)
        {
            pk = Marshal.PtrToStringUTF8(pkPtr) ?? string.Empty;
            NativeMethods.zvec_free(pkPtr);
        }

        // 2. Get Score
        float score = NativeMethods.zvec_doc_get_score(docPtr);

        var fields = new Dictionary<string, object>();
        var denseVectors = new Dictionary<string, ReadOnlyMemory<float>>();
        var sparseVectors = new Dictionary<string, IReadOnlyDictionary<int, float>>();

        // 3. Extract Fields
        int rc = NativeMethods.zvec_doc_get_field_names(docPtr, out nint namesPtr, out nuint count);
        if (rc != 0 || namesPtr == IntPtr.Zero || count == 0) 
        {
            return new ZVecDoc { Id = pk, Score = score, Fields = fields, DenseVectors = denseVectors, SparseVectors = sparseVectors };
        }

        try
        {
            var ptrArray = new nint[count];
            Marshal.Copy(namesPtr, ptrArray, 0, (int)count);

            for (int i = 0; i < (int)count; i++)
            {
                string fieldName = Marshal.PtrToStringUTF8(ptrArray[i])!;
                
                // Determine Data Type from Schema
                ZVecDataType? dataType = null;
                if (schema != null)
                {
                    var scalarField = schema.Fields.FirstOrDefault(f => f.Name == fieldName);
                    if (scalarField != null) dataType = scalarField.DataType;
                    else
                    {
                        var vectorField = schema.Vectors.FirstOrDefault(v => v.Name == fieldName);
                        if (vectorField != null) dataType = vectorField.DataType;
                    }
                }

                if (!dataType.HasValue) continue; // Cannot unmarshal without knowing the type

                ExtractFieldValue(docPtr, fieldName, dataType.Value, fields, denseVectors, sparseVectors);
            }
        }
        finally
        {
            // Note: c_api.h doesn't define zvec_string_array_free.
            // Typically zvec_free handles array pointers if the inner strings are owned by the doc.
            NativeMethods.zvec_free(namesPtr);
        }

        return new ZVecDoc { Id = pk, Score = score, Fields = fields, DenseVectors = denseVectors, SparseVectors = sparseVectors };
    }

    private static unsafe void ExtractFieldValue(
        nint docPtr, 
        string fieldName, 
        ZVecDataType dataType, 
        Dictionary<string, object> fields,
        Dictionary<string, ReadOnlyMemory<float>> denseVectors,
        Dictionary<string, IReadOnlyDictionary<int, float>> sparseVectors)
    {
        // zvec_doc_get_field_value_pointer provides zero-copy access
        var rc = NativeMethods.zvec_doc_get_field_value_pointer(docPtr, fieldName, (int)dataType, out nint valuePtr);
        if (rc != 0 || valuePtr == IntPtr.Zero) return;

        var val = Marshal.PtrToStructure<ZVecFieldValue>(valuePtr);

        switch (dataType)
        {
            case ZVecDataType.Bool:
                fields[fieldName] = val.BoolValue;
                break;
            case ZVecDataType.Int32:
                fields[fieldName] = val.Int32Value;
                break;
            case ZVecDataType.Int64:
                fields[fieldName] = val.Int64Value;
                break;
            case ZVecDataType.UInt32:
                fields[fieldName] = val.Uint32Value;
                break;
            case ZVecDataType.UInt64:
                fields[fieldName] = val.Uint64Value;
                break;
            case ZVecDataType.Float:
                fields[fieldName] = val.FloatValue;
                break;
            case ZVecDataType.Double:
                fields[fieldName] = val.DoubleValue;
                break;
            case ZVecDataType.String:
                if (val.StringValue.Str != IntPtr.Zero)
                {
                    // Convert UTF-8 bytes back to C# string
                    fields[fieldName] = Marshal.PtrToStringUTF8(val.StringValue.Str, (int)val.StringValue.Len) ?? string.Empty;
                }
                break;
            case ZVecDataType.VectorFp32:
                if (val.VectorValue.Data != IntPtr.Zero)
                {
                    // Use AllocateUninitializedArray to skip zeroing the float array
                    float[] floatArray = GC.AllocateUninitializedArray<float>((int)val.VectorValue.Len);
                    Marshal.Copy(val.VectorValue.Data, floatArray, 0, (int)val.VectorValue.Len);
                    denseVectors[fieldName] = floatArray;
                }
                break;
            case ZVecDataType.SparseVectorFp32:
                int rcSparse = NativeMethods.zvec_doc_get_sparse_vector_field(docPtr, fieldName, out nint indicesPtr, out nint valuesPtr, out nuint sparseCount);
                if (rcSparse == 0 && sparseCount > 0)
                {
                    var dict = new Dictionary<int, float>((int)sparseCount);
                    int* indices = (int*)indicesPtr;
                    float* values = (float*)valuesPtr;
                    for (int i = 0; i < (int)sparseCount; i++)
                    {
                        dict[indices[i]] = values[i];
                    }
                    sparseVectors[fieldName] = dict;
                }
                break;
            // Additional types can be implemented as needed
        }
    }
}
