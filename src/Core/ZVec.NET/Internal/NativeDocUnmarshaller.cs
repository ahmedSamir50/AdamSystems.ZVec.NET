using System.Runtime.InteropServices;
using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

internal static class NativeDocUnmarshaller
{
    public static unsafe ZVecDoc Unmarshal(
        nint docPtr,
        IReadOnlyDictionary<string, ZVecDataType>? fieldTypeMap,
        bool includeVector = true)
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

        var fields = new Dictionary<string, object>(StringComparer.Ordinal);
        Dictionary<string, ReadOnlyMemory<float>>? denseVectors = includeVector
            ? new Dictionary<string, ReadOnlyMemory<float>>(StringComparer.Ordinal)
            : null;
        Dictionary<string, IReadOnlyDictionary<int, float>>? sparseVectors = includeVector
            ? new Dictionary<string, IReadOnlyDictionary<int, float>>(StringComparer.Ordinal)
            : null;

        int rc = NativeMethods.zvec_doc_get_field_names(docPtr, out nint namesPtr, out nuint count);
        if (rc != 0 || namesPtr == IntPtr.Zero || count == 0)
        {
            return new ZVecDoc
            {
                Id = pk,
                Score = score,
                Fields = fields,
                DenseVectors = denseVectors ?? EmptyDense,
                SparseVectors = sparseVectors ?? EmptySparse
            };
        }

        try
        {
            nint* namePtrs = (nint*)namesPtr;
            for (int i = 0; i < (int)count; i++)
            {
                string fieldName = Marshal.PtrToStringUTF8(namePtrs[i])!;
                if (fieldTypeMap is null || !fieldTypeMap.TryGetValue(fieldName, out ZVecDataType dataType))
                    continue;

                if (!includeVector && IsVectorDataType(dataType))
                    continue;

                ExtractFieldValue(docPtr, fieldName, dataType, fields, denseVectors, sparseVectors);
            }
        }
        finally
        {
            NativeMethods.zvec_free_str_array(namesPtr, count);
        }

        return new ZVecDoc
        {
            Id = pk,
            Score = score,
            Fields = fields,
            DenseVectors = denseVectors ?? EmptyDense,
            SparseVectors = sparseVectors ?? EmptySparse
        };
    }

    /// <summary>Legacy overload used when only a schema DTO is available (builds a one-shot map).</summary>
    public static ZVecDoc Unmarshal(nint docPtr, ZVecCollectionSchema? schema) =>
        Unmarshal(docPtr, BuildMap(schema), includeVector: true);

    private static readonly IReadOnlyDictionary<string, ReadOnlyMemory<float>> EmptyDense =
        new Dictionary<string, ReadOnlyMemory<float>>();

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> EmptySparse =
        new Dictionary<string, IReadOnlyDictionary<int, float>>();

    internal static bool IsVectorDataType(ZVecDataType dataType) =>
        dataType is >= ZVecDataType.VectorBinary32 and <= ZVecDataType.VectorInt16
            or >= ZVecDataType.SparseVectorFp16 and <= ZVecDataType.SparseVectorFp32;

    private static Dictionary<string, ZVecDataType>? BuildMap(ZVecCollectionSchema? schema)
    {
        if (schema is null)
            return null;

        var map = new Dictionary<string, ZVecDataType>(
            schema.Fields.Count + schema.Vectors.Count, StringComparer.Ordinal);
        foreach (var f in schema.Fields)
            map[f.Name] = f.DataType;
        foreach (var v in schema.Vectors)
            map[v.Name] = v.DataType;
        return map;
    }

    private static unsafe void ExtractFieldValue(
        nint docPtr,
        string fieldName,
        ZVecDataType dataType,
        Dictionary<string, object> fields,
        Dictionary<string, ReadOnlyMemory<float>>? denseVectors,
        Dictionary<string, IReadOnlyDictionary<int, float>>? sparseVectors)
    {
        if (dataType == ZVecDataType.SparseVectorFp32)
        {
            if (sparseVectors is null)
                return;

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
                if (valueSize > 0)
                    fields[fieldName] = Marshal.PtrToStringUTF8(valuePtr, (int)valueSize) ?? string.Empty;
                else
                    fields[fieldName] = string.Empty;
                break;
            case ZVecDataType.VectorFp32:
                if (denseVectors is null)
                    return;
                if (valueSize > 0)
                {
                    int floatCount = (int)(valueSize / sizeof(float));
                    float[] floatArray = GC.AllocateUninitializedArray<float>(floatCount);
                    Marshal.Copy(valuePtr, floatArray, 0, floatCount);
                    denseVectors[fieldName] = floatArray;
                }
                break;
        }
    }
}
