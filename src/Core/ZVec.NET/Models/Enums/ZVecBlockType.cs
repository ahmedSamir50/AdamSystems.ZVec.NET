namespace ZVec.NET;

/// <summary>Maps to <c>zvec::BlockType</c> in <c>type.h</c>.</summary>
public enum ZVecBlockType
{
    Undefined = 0,
    Scalar = 1,
    ScalarIndex = 2,
    VectorIndex = 3,
    VectorIndexQuantize = 4,
    FtsIndex = 5
}
