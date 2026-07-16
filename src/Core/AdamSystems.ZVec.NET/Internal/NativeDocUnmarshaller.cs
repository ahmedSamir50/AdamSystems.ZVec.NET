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
            NativeMethods.zvec_free_str_array(namesPtr, count);
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
        // The real C API returns a raw pointer into document memory + the byte size of the value.
        // For sparse vectors we use the dedicated sparse API instead.
        if (dataType == ZVecDataType.SparseVectorFp32)
        {
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
            return;
        }

        // zvec_doc_get_field_value_pointer returns (const void** value, size_t* value_size)
        var rc = NativeMethods.zvec_doc_get_field_value_pointer(docPtr, fieldName, (int)dataType, out nint valuePtr, out nuint valueSize);
        if (rc != 0 || valuePtr == IntPtr.Zero) return;

        switch (dataType)
        {
            case ZVecDataType.Bool:
                fields[fieldName] = *(bool*)valuePtr;
                break;
            case ZVecDataType.Int32:
                fields[fieldName] = *(int*)valuePtr;
                break;
            case ZVecDataType.Int64:
                fields[fieldName] = *(long*)valuePtr;
                break;
            case ZVecDataType.UInt32:
                fields[fieldName] = *(uint*)valuePtr;
                break;
            case ZVecDataType.UInt64:
                fields[fieldName] = *(ulong*)valuePtr;
                break;
            case ZVecDataType.Float:
                fields[fieldName] = *(float*)valuePtr;
                break;
            case ZVecDataType.Double:
                fields[fieldName] = *(double*)valuePtr;
                break;
            case ZVecDataType.String:
                // valuePtr points to the UTF-8 string data, valueSize is its byte length
                if (valueSize > 0)
                    fields[fieldName] = Marshal.PtrToStringUTF8(valuePtr, (int)valueSize) ?? string.Empty;
                else
                    fields[fieldName] = string.Empty;
                break;
            case ZVecDataType.VectorFp32:
                // valuePtr is float*, valueSize is byte count → element count = valueSize / 4
                if (valuePtr != IntPtr.Zero && valueSize > 0)
                {
                    int floatCount = (int)(valueSize / sizeof(float));
                    float[] floatArray = GC.AllocateUninitializedArray<float>(floatCount);
                    Marshal.Copy(valuePtr, floatArray, 0, floatCount);
                    denseVectors[fieldName] = floatArray;
                }
                break;
            // Additional types can be implemented as needed
        }
    }
}
