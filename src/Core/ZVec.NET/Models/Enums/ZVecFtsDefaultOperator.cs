namespace ZVec.NET;

/// <summary>
/// Specifies the default logical operator used by the native FTS engine when parsing multiple query terms.
/// If not specified, the native library (e.g., FTS5 backend) typically defaults to <see cref="Or"/>.
/// Used by <c>ZVecFtsQuery.DefaultOperator</c>; maps to FTS query strings "OR" / "AND".
/// </summary>
public enum ZVecFtsDefaultOperator
{
    Or = 0,
    And = 1
}
