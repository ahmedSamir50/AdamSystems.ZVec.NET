using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Mapping;

/// <summary>Resolves mapped properties from member-access expressions (reuse seam).</summary>
internal static class ZVecMemberPath
{
    public static ZVecMappedProperty ResolveProperty<T, TProp>(
        ZVecTypeModel model,
        Expression<Func<T, TProp>> expression)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(expression);

        var property = TryGetPropertyInfo(expression.Body)
            ?? throw new ZVecException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    ZVecDefaults.Errors.MappingMemberExpressionRequired,
                    typeof(T).Name));

        return model.GetRequiredByPropertyName(property.Name);
    }

    public static PropertyInfo? TryGetPropertyInfo(Expression body)
    {
        body = Unwrap(body);
        if (body is MemberExpression { Member: PropertyInfo property })
            return property;
        return null;
    }

    public static Expression Unwrap(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;
        return expression;
    }
}
