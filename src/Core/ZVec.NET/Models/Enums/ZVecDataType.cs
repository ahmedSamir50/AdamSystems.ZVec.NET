namespace ZVec.NET;

/// <summary>Maps to <c>zvec::DataType</c> / <c>ZVEC_DATA_TYPE_*</c>.</summary>
public enum ZVecDataType
{
    Undefined = 0,
    Binary = 1,
    String = 2,
    Bool = 3,
    Int32 = 4,
    Int64 = 5,
    UInt32 = 6,
    UInt64 = 7,
    Float = 8,
    Double = 9,
    VectorBinary32 = 20,
    VectorBinary64 = 21,
    VectorFp16 = 22,
    VectorFp32 = 23,
    VectorFp64 = 24,
    VectorInt4 = 25,
    VectorInt8 = 26,
    VectorInt16 = 27,
    SparseVectorFp16 = 30,
    SparseVectorFp32 = 31,
    ArrayBinary = 40,
    ArrayString = 41,
    ArrayBool = 42,
    ArrayInt32 = 43,
    ArrayInt64 = 44,
    ArrayUInt32 = 45,
    ArrayUInt64 = 46,
    ArrayFloat = 47,
    ArrayDouble = 48
}
