namespace ZVec.NET;

/// <summary>Maps to <c>zvec::CompareOp</c> in <c>type.h</c>.</summary>
public enum ZVecCompareOp
{
    None = 0,
    Eq = 1,
    Ne = 2,
    Lt = 3,
    Le = 4,
    Gt = 5,
    Ge = 6,
    Like = 7,
    ContainAll = 8,
    ContainAny = 9,
    NotContainAll = 10,
    NotContainAny = 11,
    IsNull = 12,
    IsNotNull = 13,
    HasPrefix = 14,
    HasSuffix = 15
}
