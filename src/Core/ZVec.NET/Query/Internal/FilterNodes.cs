using System.Globalization;
using System.Text;

namespace ZVec.NET.Query.Internal;

/// <summary>Immutable AST node for ZVec filter expressions.</summary>
internal abstract class FilterNode
{
    public abstract string Render();
    public abstract FilterNode Negate();
    public virtual bool IsEmpty => false;
}

internal sealed class EmptyFilterNode : FilterNode
{
    public static readonly EmptyFilterNode Instance = new();
    private EmptyFilterNode() { }
    public override bool IsEmpty => true;
    public override string Render() => string.Empty;
    public override FilterNode Negate() =>
        throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported);
}

internal sealed class ComparisonFilterNode : FilterNode
{
    public ComparisonFilterNode(string fieldName, ZVecCompareOp op, string formattedValue)
    {
        FieldName = fieldName;
        Op = op;
        FormattedValue = formattedValue;
    }

    public string FieldName { get; }
    public ZVecCompareOp Op { get; }
    public string FormattedValue { get; }

    public override string Render() => string.Concat(
        FieldName,
        ZVecDefaults.Filter.Space,
        RelationalOpToString(Op),
        ZVecDefaults.Filter.Space,
        FormattedValue);

    public override FilterNode Negate() => new ComparisonFilterNode(FieldName, FlipRelationalOp(Op), FormattedValue);

    internal static string RelationalOpToString(ZVecCompareOp op) => op switch
    {
        ZVecCompareOp.Eq => ZVecDefaults.Filter.Eq,
        ZVecCompareOp.Ne => ZVecDefaults.Filter.Ne,
        ZVecCompareOp.Gt => ZVecDefaults.Filter.Gt,
        ZVecCompareOp.Lt => ZVecDefaults.Filter.Lt,
        ZVecCompareOp.Ge => ZVecDefaults.Filter.Ge,
        ZVecCompareOp.Le => ZVecDefaults.Filter.Le,
        _ => throw new ArgumentOutOfRangeException(
            nameof(op),
            string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.UnsupportedWhereCompareOp, op))
    };

    private static ZVecCompareOp FlipRelationalOp(ZVecCompareOp op) => op switch
    {
        ZVecCompareOp.Eq => ZVecCompareOp.Ne,
        ZVecCompareOp.Ne => ZVecCompareOp.Eq,
        ZVecCompareOp.Gt => ZVecCompareOp.Le,
        ZVecCompareOp.Lt => ZVecCompareOp.Ge,
        ZVecCompareOp.Ge => ZVecCompareOp.Lt,
        ZVecCompareOp.Le => ZVecCompareOp.Gt,
        _ => throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported)
    };
}

internal sealed class InFilterNode : FilterNode
{
    public InFilterNode(string fieldName, string formattedValues, bool negated = false)
    {
        FieldName = fieldName;
        FormattedValues = formattedValues;
        Negated = negated;
    }

    public string FieldName { get; }
    public string FormattedValues { get; }
    public bool Negated { get; }

    public override string Render() => string.Concat(
        FieldName,
        ZVecDefaults.Filter.Space,
        Negated
            ? string.Concat(ZVecDefaults.Filter.Not, ZVecDefaults.Filter.Space, ZVecDefaults.Filter.In)
            : ZVecDefaults.Filter.In,
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.OpenParen,
        FormattedValues,
        ZVecDefaults.Filter.CloseParen);

    public override FilterNode Negate() => new InFilterNode(FieldName, FormattedValues, !Negated);
}

internal sealed class LikeFilterNode : FilterNode
{
    public LikeFilterNode(string fieldName, string formattedPattern)
    {
        FieldName = fieldName;
        FormattedPattern = formattedPattern;
    }

    public string FieldName { get; }
    public string FormattedPattern { get; }

    public override string Render() => string.Concat(
        FieldName,
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.Like,
        ZVecDefaults.Filter.Space,
        FormattedPattern);

    public override FilterNode Negate() =>
        throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported);
}

internal sealed class ContainFilterNode : FilterNode
{
    public ContainFilterNode(string fieldName, string keyword, string formattedValues, bool negated = false)
    {
        FieldName = fieldName;
        Keyword = keyword;
        FormattedValues = formattedValues;
        Negated = negated;
    }

    public string FieldName { get; }
    public string Keyword { get; }
    public string FormattedValues { get; }
    public bool Negated { get; }

    public override string Render() => string.Concat(
        FieldName,
        ZVecDefaults.Filter.Space,
        Negated
            ? string.Concat(ZVecDefaults.Filter.Not, ZVecDefaults.Filter.Space, Keyword)
            : Keyword,
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.OpenParen,
        FormattedValues,
        ZVecDefaults.Filter.CloseParen);

    public override FilterNode Negate() => new ContainFilterNode(FieldName, Keyword, FormattedValues, !Negated);
}

internal sealed class IsNullFilterNode : FilterNode
{
    public IsNullFilterNode(string fieldName, bool isNotNull = false)
    {
        FieldName = fieldName;
        IsNotNull = isNotNull;
    }

    public string FieldName { get; }
    public bool IsNotNull { get; }

    public override string Render() => string.Concat(
        FieldName,
        ZVecDefaults.Filter.Space,
        IsNotNull ? ZVecDefaults.Filter.IsNotNull : ZVecDefaults.Filter.IsNull);

    public override FilterNode Negate() => new IsNullFilterNode(FieldName, !IsNotNull);
}

internal sealed class AndFilterNode : FilterNode
{
    public AndFilterNode(FilterNode left, FilterNode right)
    {
        Left = left;
        Right = right;
    }

    public FilterNode Left { get; }
    public FilterNode Right { get; }

    public override string Render() => string.Concat(
        Left.Render(),
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.And,
        ZVecDefaults.Filter.Space,
        Right.Render());

    public override FilterNode Negate() => new OrFilterNode(Left.Negate(), Right.Negate());
}

internal sealed class OrFilterNode : FilterNode
{
    public OrFilterNode(FilterNode left, FilterNode right)
    {
        Left = left;
        Right = right;
    }

    public FilterNode Left { get; }
    public FilterNode Right { get; }

    public override string Render() => string.Concat(
        ZVecDefaults.Filter.OpenParen,
        Left.Render(),
        ZVecDefaults.Filter.CloseParen,
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.Or,
        ZVecDefaults.Filter.Space,
        ZVecDefaults.Filter.OpenParen,
        Right.Render(),
        ZVecDefaults.Filter.CloseParen);

    public override FilterNode Negate() => new AndFilterNode(Left.Negate(), Right.Negate());
}

internal sealed class NotFilterNode : FilterNode
{
    public NotFilterNode(FilterNode inner) => Inner = inner;
    public FilterNode Inner { get; }
    public override string Render() => Inner.Negate().Render();
    public override FilterNode Negate() => Inner;
}

internal static class FilterValueFormatter
{
    public static string FormatValueList(object[] values)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(ZVecDefaults.Filter.CommaSpace);
            sb.Append(FormatValue(values[i]));
        }
        return sb.ToString();
    }

    public static string FormatValue(object? value) => value switch
    {
        null => ZVecDefaults.Filter.Null,
        string s => FormatString(s),
        bool b => b ? ZVecDefaults.Filter.True : ZVecDefaults.Filter.False,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? ZVecDefaults.Filter.Null,
        _ => value.ToString() ?? ZVecDefaults.Filter.Null
    };

    public static string FormatString(string value)
        => string.Concat(
            ZVecDefaults.Filter.DoubleQuote,
            EscapeString(value),
            ZVecDefaults.Filter.DoubleQuote);

    public static string EscapeString(string value)
        => value
            .Replace(
                ZVecDefaults.Filter.Backslash.ToString(),
                string.Concat(ZVecDefaults.Filter.Backslash, ZVecDefaults.Filter.Backslash))
            .Replace(
                ZVecDefaults.Filter.SingleQuote.ToString(),
                string.Concat(ZVecDefaults.Filter.Backslash, ZVecDefaults.Filter.SingleQuote))
            .Replace(
                ZVecDefaults.Filter.DoubleQuote.ToString(),
                string.Concat(ZVecDefaults.Filter.Backslash, ZVecDefaults.Filter.DoubleQuote));

    public static string FormatNumber<T>(T value) where T : IFormattable
        => value.ToString(null, CultureInfo.InvariantCulture)!;
}
