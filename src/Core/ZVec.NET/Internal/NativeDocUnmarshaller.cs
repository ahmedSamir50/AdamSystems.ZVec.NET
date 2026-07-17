using System.Runtime.InteropServices;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

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
            // CRITICAL: zvec_doc_get_field_names allocates an array of string pointers (char**) where EACH inner string is also allocated.
            // Using a plain zvec_free(namesPtr) would leak all the inner strings and corrupt heap tracking.
            // We MUST use zvec_free_str_array to properly free both the outer array and the inner strings.
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
        // Sparse FP32: only zvec_doc_get_field_value_copy supports SPARSE_VECTOR_*.
        // Layout from c_api.cc: [nnz:size_t][uint32 indices...][float values...] (caller frees).
        if (dataType == ZVecDataType.SparseVectorFp32)
        {
            var rcSparse = NativeMethods.zvec_doc_get_field_value_copy(
                docPtr, fieldName, (int)dataType, out nint sparsePtr, out nuint sparseSize);
            if (rcSparse != 0 || sparsePtr == IntPtr.Zero || sparseSize < (nuint)sizeof(nuint))
                return;

            try
            {
                byte* basePtr = (byte*)sparsePtr;
                nuint nnz = *(nuint*)basePtr;
                nuint required = (nuint)sizeof(nuint) + nnz * (nuint)(sizeof(uint) + sizeof(float));
                if (sparseSize < required || nnz == 0)
                {
                    sparseVectors[fieldName] = new Dictionary<int, float>();
                    return;
                }

                uint* indices = (uint*)(basePtr + sizeof(nuint));
                float* values = (float*)(indices + nnz);
                var dict = new Dictionary<int, float>((int)nnz);
                for (nuint i = 0; i < nnz; i++)
                    dict[(int)indices[i]] = values[i];
                sparseVectors[fieldName] = dict;
            }
            finally
            {
                NativeMethods.zvec_free(sparsePtr);
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
