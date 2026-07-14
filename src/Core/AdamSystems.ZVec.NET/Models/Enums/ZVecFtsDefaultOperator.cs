namespace AdamSystems.ZVec.NET;

/// <summary>
/// Managed-only FTS default operator (not a native ABI enum).
/// Used by <c>ZVecFtsQuery.DefaultOperator</c>; maps to FTS query strings "OR" / "AND".
/// </summary>
public enum ZVecFtsDefaultOperator
{
    Or = 0,
    And = 1
}
