using System.Globalization;
using System.Linq.Expressions;
using ZVec.NET.Exceptions;
using ZVec.NET.Query;

namespace ZVec.NET.Mapping;

/// <summary>
/// Translates typed boolean expressions into native ZVec filter strings.
/// Shared engine — callable from future adapters without a second visitor.
/// </summary>
/// <remarks>
/// <para>
/// Supported: comparisons (<c>==</c> <c>!=</c> <c>&lt;</c> <c>&lt;=</c> <c>&gt;</c> <c>&gt;=</c>,
/// constant on either side), boolean trees (<c>&amp;&amp;</c> <c>||</c> <c>!</c>), and null checks.
/// Property names resolve through <see cref="ZVecTypeModel"/> (including <see cref="ZVecFieldAttribute"/> storage overrides).
/// </para>
/// <para>
/// Unsupported shapes (method calls such as <c>StartsWith</c>, indexers, etc.) throw <see cref="ZVecException"/>.
/// Use <see cref="IZvecCollection{T}.Untyped"/> with <see cref="ZVecFilterBuilder"/> for advanced filters.
/// </para>
/// </remarks>
public static class ZVecExpressionFilter
{
    /// <summary>
    /// Translates <paramref name="filter"/> for type <typeparamref name="T"/> into a native filter string.
    /// </summary>
    /// <typeparam name="T">Mapped document type.</typeparam>
    /// <param name="filter">Boolean expression over <typeparamref name="T"/> properties.</param>
    /// <returns>Native ZVec filter string.</returns>
    public static string Translate<T>(Expression<Func<T, bool>> filter) where T : class
    {
        ArgumentNullException.ThrowIfNull(filter);
        var model = ZVecTypeModel.Get<T>();
        return Translate(model, filter);
    }

    /// <summary>
    /// Translates using a precomputed type model (reuse seam).
    /// </summary>
    /// <typeparam name="T">Mapped document type.</typeparam>
    /// <param name="model">Cached mapping metadata for <typeparamref name="T"/>.</param>
    /// <param name="filter">Boolean expression over <typeparamref name="T"/> properties.</param>
    /// <returns>Native ZVec filter string.</returns>
    public static string Translate<T>(ZVecTypeModel model, Expression<Func<T, bool>> filter) where T : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(filter);

        var builder = Visit(model, filter.Body, ZVecFilterBuilder.Create());
        return builder.Build();
    }

    private static ZVecFilterBuilder Visit(ZVecTypeModel model, Expression body, ZVecFilterBuilder current)
    {
        body = ZVecMemberPath.Unwrap(body);

        switch (body)
        {
            case BinaryExpression binary:
                return VisitBinary(model, binary, current);

            case UnaryExpression { NodeType: ExpressionType.Not } unary:
                var inner = Visit(model, unary.Operand, ZVecFilterBuilder.Create());
                return current.Not(inner);

            case ConstantExpression { Value: true }:
                return current;

            case ConstantExpression { Value: false }:
                throw new ZVecException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        ZVecDefaults.Errors.MappingExpressionUnsupported,
                        "constant false"));

            default:
                throw Unsupported(body);
        }
    }

    private static ZVecFilterBuilder VisitBinary(ZVecTypeModel model, BinaryExpression binary, ZVecFilterBuilder current)
    {
        if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.And)
        {
            var left = Visit(model, binary.Left, ZVecFilterBuilder.Create());
            var right = Visit(model, binary.Right, ZVecFilterBuilder.Create());
            var combined = left.And(right);
            return current.Build().Length == 0 ? combined : current.And(combined);
        }

        if (binary.NodeType is ExpressionType.OrElse or ExpressionType.Or)
        {
            var left = Visit(model, binary.Left, ZVecFilterBuilder.Create());
            var right = Visit(model, binary.Right, ZVecFilterBuilder.Create());
            var combined = left.Or(right);
            return current.Build().Length == 0 ? combined : current.And(combined);
        }

        return AppendComparison(model, binary, current);
    }

    private static ZVecFilterBuilder AppendComparison(ZVecTypeModel model, BinaryExpression binary, ZVecFilterBuilder current)
    {
        var (memberSide, valueSide, invert) = SplitComparison(binary);

        if (IsNullConstant(valueSide))
        {
            var nullProp = ResolveMember(model, memberSide);
            return binary.NodeType switch
            {
                ExpressionType.Equal => invert ? current.IsNotNull(nullProp.StorageName) : current.IsNull(nullProp.StorageName),
                ExpressionType.NotEqual => invert ? current.IsNull(nullProp.StorageName) : current.IsNotNull(nullProp.StorageName),
                _ => throw Unsupported(binary)
            };
        }

        var property = ResolveMember(model, memberSide);
        var value = Evaluate(valueSide);
        var op = MapCompareOp(binary.NodeType, invert);

        return value switch
        {
            null => op == ZVecCompareOp.Eq ? current.IsNull(property.StorageName) : current.IsNotNull(property.StorageName),
            string s => current.Where(property.StorageName, op, s),
            bool b => current.Where(property.StorageName, op, b),
            int i => current.Where(property.StorageName, op, i),
            long l => current.Where(property.StorageName, op, l),
            float f => current.Where(property.StorageName, op, f),
            double d => current.Where(property.StorageName, op, d),
            uint ui => current.Where(property.StorageName, op, (long)ui),
            ulong ul when ul <= long.MaxValue => current.Where(property.StorageName, op, (long)ul),
            _ => current.Where(property.StorageName, op, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
        };
    }

    private static (Expression member, Expression value, bool invert) SplitComparison(BinaryExpression binary)
    {
        if (ZVecMemberPath.TryGetPropertyInfo(binary.Left) is not null)
            return (binary.Left, binary.Right, invert: false);
        if (ZVecMemberPath.TryGetPropertyInfo(binary.Right) is not null)
            return (binary.Right, binary.Left, invert: true);
        throw Unsupported(binary);
    }

    private static ZVecMappedProperty ResolveMember(ZVecTypeModel model, Expression memberExpression)
    {
        var propertyInfo = ZVecMemberPath.TryGetPropertyInfo(memberExpression)
            ?? throw Unsupported(memberExpression);
        return model.GetRequiredByPropertyName(propertyInfo.Name);
    }

    private static ZVecCompareOp MapCompareOp(ExpressionType nodeType, bool invertSides)
    {
        // When the constant is on the left (x < p.Age), relational ops flip.
        return (nodeType, invertSides) switch
        {
            (ExpressionType.Equal, _) => ZVecCompareOp.Eq,
            (ExpressionType.NotEqual, _) => ZVecCompareOp.Ne,
            (ExpressionType.GreaterThan, false) => ZVecCompareOp.Gt,
            (ExpressionType.GreaterThanOrEqual, false) => ZVecCompareOp.Ge,
            (ExpressionType.LessThan, false) => ZVecCompareOp.Lt,
            (ExpressionType.LessThanOrEqual, false) => ZVecCompareOp.Le,
            (ExpressionType.GreaterThan, true) => ZVecCompareOp.Lt,
            (ExpressionType.GreaterThanOrEqual, true) => ZVecCompareOp.Le,
            (ExpressionType.LessThan, true) => ZVecCompareOp.Gt,
            (ExpressionType.LessThanOrEqual, true) => ZVecCompareOp.Ge,
            _ => throw new ZVecException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingExpressionUnsupported,
                    nodeType))
        };
    }

    private static bool IsNullConstant(Expression expression)
        => expression is ConstantExpression { Value: null };

    private static object? Evaluate(Expression expression)
    {
        expression = ZVecMemberPath.Unwrap(expression);
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }

    private static ZVecException Unsupported(Expression expression)
        => new(string.Format(
            CultureInfo.InvariantCulture,
            ZVecDefaults.Errors.MappingExpressionUnsupported,
            expression.NodeType));
}
