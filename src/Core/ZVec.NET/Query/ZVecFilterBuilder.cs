using System.Globalization;
using System.Text.RegularExpressions;
using ZVec.NET.Query.Internal;

namespace ZVec.NET.Query;

/// <summary>
/// Immutable fluent builder for ZVec filter expressions.
/// Builds an AST and renders a native filter string via <see cref="Build"/>.
/// Nest with lambda <c>And</c>/<c>Or</c>/<c>Not</c> to avoid repeated <see cref="Create"/> calls.
/// </summary>
public sealed partial class ZVecFilterBuilder
{
    private readonly FilterNode _root;

    private ZVecFilterBuilder(FilterNode root) => _root = root;

    /// <summary>Creates an empty filter builder.</summary>
    public static ZVecFilterBuilder Create() => new(EmptyFilterNode.Instance);

    /// <inheritdoc cref="Where(string, ZVecCompareOp, int)"/>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, int value)
        => AppendComparison(fieldName, op, FilterValueFormatter.FormatNumber(value));

    /// <inheritdoc cref="Where(string, ZVecCompareOp, long)"/>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, long value)
        => AppendComparison(fieldName, op, FilterValueFormatter.FormatNumber(value));

    /// <inheritdoc cref="Where(string, ZVecCompareOp, float)"/>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, float value)
        => AppendComparison(fieldName, op, FilterValueFormatter.FormatNumber(value));

    /// <inheritdoc cref="Where(string, ZVecCompareOp, double)"/>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, double value)
        => AppendComparison(fieldName, op, FilterValueFormatter.FormatNumber(value));

    /// <summary>Adds a relational comparison against a string value (ANDed if non-empty).</summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return AppendComparison(fieldName, op, FilterValueFormatter.FormatString(value));
    }

    /// <summary>Adds a relational comparison against a boolean value.</summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, bool value)
        => AppendComparison(fieldName, op, value ? ZVecDefaults.Filter.True : ZVecDefaults.Filter.False);

    /// <summary>Adds an IN clause (ANDed if the builder is non-empty).</summary>
    public ZVecFilterBuilder In(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        return AppendNode(new InFilterNode(fieldName, FilterValueFormatter.FormatValueList(values)));
    }

    /// <summary>Adds a LIKE clause.</summary>
    public ZVecFilterBuilder Like(string fieldName, string pattern)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(pattern);
        return AppendNode(new LikeFilterNode(fieldName, FilterValueFormatter.FormatString(pattern)));
    }

    /// <summary>Adds a CONTAIN_ANY clause.</summary>
    public ZVecFilterBuilder ContainAny(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        return AppendNode(new ContainFilterNode(
            fieldName,
            ZVecDefaults.Filter.ContainAny,
            FilterValueFormatter.FormatValueList(values)));
    }

    /// <summary>Adds a CONTAIN_ALL clause.</summary>
    public ZVecFilterBuilder ContainAll(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        return AppendNode(new ContainFilterNode(
            fieldName,
            ZVecDefaults.Filter.ContainAll,
            FilterValueFormatter.FormatValueList(values)));
    }

    /// <summary>Adds an IS NULL predicate.</summary>
    public ZVecFilterBuilder IsNull(string fieldName)
    {
        ValidateFieldName(fieldName);
        return AppendNode(new IsNullFilterNode(fieldName));
    }

    /// <summary>Adds an IS NOT NULL predicate.</summary>
    public ZVecFilterBuilder IsNotNull(string fieldName)
    {
        ValidateFieldName(fieldName);
        return AppendNode(new IsNullFilterNode(fieldName, isNotNull: true));
    }

    /// <summary>Logical AND with another filter. Empty left absorbs right.</summary>
    public ZVecFilterBuilder And(ZVecFilterBuilder other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._root.IsEmpty)
            throw new ArgumentException(ZVecDefaults.Errors.FilterEmptyRightOperand, nameof(other));
        if (_root.IsEmpty)
            return other;
        return new ZVecFilterBuilder(new AndFilterNode(_root, other._root));
    }

    /// <summary>Logical AND with a nested builder (no nested Create required).</summary>
    public ZVecFilterBuilder And(Func<ZVecFilterBuilder, ZVecFilterBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return And(build(Create()));
    }

    /// <summary>Logical OR with another filter. Empty left absorbs right.</summary>
    public ZVecFilterBuilder Or(ZVecFilterBuilder other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._root.IsEmpty)
            throw new ArgumentException(ZVecDefaults.Errors.FilterEmptyRightOperand, nameof(other));
        if (_root.IsEmpty)
            return other;
        return new ZVecFilterBuilder(new OrFilterNode(_root, other._root));
    }

    /// <summary>Logical OR with a nested builder.</summary>
    public ZVecFilterBuilder Or(Func<ZVecFilterBuilder, ZVecFilterBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return Or(build(Create()));
    }

    /// <summary>
    /// Negates <paramref name="other"/> and ANDs it with the current filter when non-empty.
    /// Negation is performed on the AST (not by scanning rendered strings).
    /// </summary>
    public ZVecFilterBuilder Not(ZVecFilterBuilder other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._root.IsEmpty)
            throw new ArgumentException(ZVecDefaults.Errors.FilterEmptyRightOperand, nameof(other));

        var negated = new ZVecFilterBuilder(new NotFilterNode(other._root));
        if (_root.IsEmpty)
            return negated;
        return And(negated);
    }

    /// <summary>Negates a nested builder and ANDs when the left side is non-empty.</summary>
    public ZVecFilterBuilder Not(Func<ZVecFilterBuilder, ZVecFilterBuilder> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return Not(build(Create()));
    }

    /// <summary>Renders the native filter expression string.</summary>
    public string Build() => _root.Render();

    /// <summary>Debugger-friendly representation including the built expression.</summary>
    public override string ToString() =>
        string.Concat(ZVecDefaults.Filter.BuilderToStringPrefix, Build());

    private ZVecFilterBuilder AppendComparison(string fieldName, ZVecCompareOp op, string formattedValue)
    {
        ValidateFieldName(fieldName);
        EnsureRelationalOp(op);
        return AppendNode(new ComparisonFilterNode(fieldName, op, formattedValue));
    }

    private ZVecFilterBuilder AppendNode(FilterNode node)
    {
        if (_root.IsEmpty)
            return new ZVecFilterBuilder(node);
        return new ZVecFilterBuilder(new AndFilterNode(_root, node));
    }

    private static void EnsureRelationalOp(ZVecCompareOp op)
    {
        switch (op)
        {
            case ZVecCompareOp.Eq:
            case ZVecCompareOp.Ne:
            case ZVecCompareOp.Lt:
            case ZVecCompareOp.Le:
            case ZVecCompareOp.Gt:
            case ZVecCompareOp.Ge:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(op),
                    string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.UnsupportedWhereCompareOp, op));
        }
    }

    private static void ValidateFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException(ZVecDefaults.Errors.FilterFieldNameRequired, nameof(fieldName));

        if (!FieldNameRegex().IsMatch(fieldName))
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.FilterFieldNameInvalid, fieldName),
                nameof(fieldName));
        }
    }

    [GeneratedRegex(ZVecDefaults.Filter.FieldNamePattern)]
    private static partial Regex FieldNameRegex();
}
