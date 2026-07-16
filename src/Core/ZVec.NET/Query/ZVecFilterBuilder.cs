using System.Globalization;
using System.Text;

namespace ZVec.NET.Query;

/// <summary>
/// A fluent builder for constructing ZVec query filter expressions.
/// String values are escaped (quotes / backslashes) so literals like O'Brien cannot break the expression.
/// </summary>
public sealed class ZVecFilterBuilder
{
    private string _expression;

    private ZVecFilterBuilder(string expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Creates a new, empty <see cref="ZVecFilterBuilder"/>.
    /// </summary>
    public static ZVecFilterBuilder Create() => new(string.Empty);

    /// <summary>
    /// Adds a comparison against an <see cref="int"/> value.
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, int value)
        => WhereCore(fieldName, op, FormatNumber(value));

    /// <summary>
    /// Adds a comparison against a <see cref="long"/> value.
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, long value)
        => WhereCore(fieldName, op, FormatNumber(value));

    /// <summary>
    /// Adds a comparison against a <see cref="float"/> value.
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, float value)
        => WhereCore(fieldName, op, FormatNumber(value));

    /// <summary>
    /// Adds a comparison against a <see cref="double"/> value.
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, double value)
        => WhereCore(fieldName, op, FormatNumber(value));

    /// <summary>
    /// Adds a comparison against a string value (escaped and quoted).
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WhereCore(fieldName, op, FormatString(value));
    }

    /// <summary>
    /// Adds a comparison against a boolean value.
    /// </summary>
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, bool value)
        => WhereCore(fieldName, op, value ? ZVecDefaults.Filter.True : ZVecDefaults.Filter.False);

    /// <summary>
    /// Chains another filter expression with a logical AND.
    /// </summary>
    public ZVecFilterBuilder And(ZVecFilterBuilder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _expression = string.Concat(
            _expression,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.And,
            ZVecDefaults.Filter.Space,
            inner._expression);
        return this;
    }

    /// <summary>
    /// Chains another filter expression with a logical OR (both sides parenthesized).
    /// </summary>
    public ZVecFilterBuilder Or(ZVecFilterBuilder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _expression = string.Concat(
            ZVecDefaults.Filter.OpenParen,
            _expression,
            ZVecDefaults.Filter.CloseParen,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Or,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            inner._expression,
            ZVecDefaults.Filter.CloseParen);
        return this;
    }

    /// <summary>
    /// Negates the given filter expression using a native-supported form
    /// (<c>!=</c>, <c>NOT IN</c>, <c>NOT CONTAIN_*</c>). Unary <c>NOT (expr)</c> is not part of the ZVec SQL grammar.
    /// </summary>
    public ZVecFilterBuilder Not(ZVecFilterBuilder inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _expression = NegateExpression(inner._expression);
        return this;
    }

    /// <summary>
    /// Adds an IN clause for the specified field.
    /// </summary>
    public ZVecFilterBuilder In(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        _expression = string.Concat(
            fieldName,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.In,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            FormatValueList(values),
            ZVecDefaults.Filter.CloseParen);
        return this;
    }

    /// <summary>
    /// Adds a LIKE clause for the specified field.
    /// </summary>
    public ZVecFilterBuilder Like(string fieldName, string pattern)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(pattern);

        _expression = string.Concat(
            fieldName,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Like,
            ZVecDefaults.Filter.Space,
            FormatString(pattern));
        return this;
    }

    /// <summary>
    /// Adds a CONTAIN_ANY clause for array/list intersection logic.
    /// </summary>
    public ZVecFilterBuilder ContainAny(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        _expression = string.Concat(
            fieldName,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAny,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            FormatValueList(values),
            ZVecDefaults.Filter.CloseParen);
        return this;
    }

    /// <summary>
    /// Adds a CONTAIN_ALL clause for array/list subset logic.
    /// </summary>
    public ZVecFilterBuilder ContainAll(string fieldName, params object[] values)
    {
        ValidateFieldName(fieldName);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException(ZVecDefaults.Errors.FilterValuesRequired, nameof(values));

        _expression = string.Concat(
            fieldName,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAll,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            FormatValueList(values),
            ZVecDefaults.Filter.CloseParen);
        return this;
    }

    /// <summary>
    /// Returns the fully constructed filter expression.
    /// </summary>
    public override string ToString() => _expression;

    private ZVecFilterBuilder WhereCore(string fieldName, ZVecCompareOp op, string formattedValue)
    {
        ValidateFieldName(fieldName);
        _expression = string.Concat(
            fieldName,
            ZVecDefaults.Filter.Space,
            OpToString(op),
            ZVecDefaults.Filter.Space,
            formattedValue);
        return this;
    }

    private static void ValidateFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException(ZVecDefaults.Errors.FilterFieldNameRequired, nameof(fieldName));
    }

    private static string OpToString(ZVecCompareOp op) => op switch
    {
        ZVecCompareOp.Eq => ZVecDefaults.Filter.Eq,
        ZVecCompareOp.Ne => ZVecDefaults.Filter.Ne,
        ZVecCompareOp.Gt => ZVecDefaults.Filter.Gt,
        ZVecCompareOp.Lt => ZVecDefaults.Filter.Lt,
        ZVecCompareOp.Ge => ZVecDefaults.Filter.Ge,
        ZVecCompareOp.Le => ZVecDefaults.Filter.Le,
        ZVecCompareOp.Like => ZVecDefaults.Filter.Like,
        ZVecCompareOp.ContainAny => ZVecDefaults.Filter.ContainAny,
        ZVecCompareOp.ContainAll => ZVecDefaults.Filter.ContainAll,
        _ => throw new ArgumentOutOfRangeException(
            nameof(op),
            string.Format(CultureInfo.InvariantCulture, ZVecDefaults.Errors.UnsupportedCompareOp, op))
    };

    /// <summary>
    /// Rewrites a simple builder-produced expression into a native-supported negation.
    /// Compound AND/OR expressions cannot be negated this way.
    /// </summary>
    private static string NegateExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported, nameof(expression));

        string andToken = string.Concat(ZVecDefaults.Filter.Space, ZVecDefaults.Filter.And, ZVecDefaults.Filter.Space);
        string orToken = string.Concat(ZVecDefaults.Filter.Space, ZVecDefaults.Filter.Or, ZVecDefaults.Filter.Space);
        if (expression.Contains(andToken, StringComparison.Ordinal)
            || expression.Contains(orToken, StringComparison.Ordinal))
        {
            throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported, nameof(expression));
        }

        string inToken = string.Concat(ZVecDefaults.Filter.Space, ZVecDefaults.Filter.In, ZVecDefaults.Filter.Space);
        string notInToken = string.Concat(
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Not,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.In,
            ZVecDefaults.Filter.Space);
        if (expression.Contains(inToken, StringComparison.Ordinal)
            && !expression.Contains(notInToken, StringComparison.Ordinal))
        {
            return expression.Replace(inToken, notInToken, StringComparison.Ordinal);
        }

        string containAnyToken = string.Concat(ZVecDefaults.Filter.Space, ZVecDefaults.Filter.ContainAny, ZVecDefaults.Filter.Space);
        string notContainAnyToken = string.Concat(
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Not,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAny,
            ZVecDefaults.Filter.Space);
        if (expression.Contains(containAnyToken, StringComparison.Ordinal)
            && !expression.Contains(notContainAnyToken, StringComparison.Ordinal))
        {
            return expression.Replace(containAnyToken, notContainAnyToken, StringComparison.Ordinal);
        }

        string containAllToken = string.Concat(ZVecDefaults.Filter.Space, ZVecDefaults.Filter.ContainAll, ZVecDefaults.Filter.Space);
        string notContainAllToken = string.Concat(
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Not,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAll,
            ZVecDefaults.Filter.Space);
        if (expression.Contains(containAllToken, StringComparison.Ordinal)
            && !expression.Contains(notContainAllToken, StringComparison.Ordinal))
        {
            return expression.Replace(containAllToken, notContainAllToken, StringComparison.Ordinal);
        }

        // Flip simple comparison operators (space-padded tokens from Where()).
        if (TryReplaceOp(expression, ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.Ne, out string? flipped)
            || TryReplaceOp(expression, ZVecDefaults.Filter.Ne, ZVecDefaults.Filter.Eq, out flipped)
            || TryReplaceOp(expression, ZVecDefaults.Filter.Gt, ZVecDefaults.Filter.Le, out flipped)
            || TryReplaceOp(expression, ZVecDefaults.Filter.Lt, ZVecDefaults.Filter.Ge, out flipped)
            || TryReplaceOp(expression, ZVecDefaults.Filter.Ge, ZVecDefaults.Filter.Lt, out flipped)
            || TryReplaceOp(expression, ZVecDefaults.Filter.Le, ZVecDefaults.Filter.Gt, out flipped))
        {
            return flipped!;
        }

        throw new ArgumentException(ZVecDefaults.Errors.FilterNotUnsupported, nameof(expression));
    }

    private static bool TryReplaceOp(string expression, string fromOp, string toOp, out string? result)
    {
        string from = string.Concat(ZVecDefaults.Filter.Space, fromOp, ZVecDefaults.Filter.Space);
        string to = string.Concat(ZVecDefaults.Filter.Space, toOp, ZVecDefaults.Filter.Space);
        int index = expression.IndexOf(from, StringComparison.Ordinal);
        if (index < 0)
        {
            result = null;
            return false;
        }

        result = string.Concat(
            expression.AsSpan(0, index),
            to,
            expression.AsSpan(index + from.Length));
        return true;
    }

    private static string FormatValueList(object[] values)
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

    private static string FormatValue(object? value) => value switch
    {
        null => ZVecDefaults.Filter.Null,
        string s => FormatString(s),
        bool b => b ? ZVecDefaults.Filter.True : ZVecDefaults.Filter.False,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? ZVecDefaults.Filter.Null,
        _ => value.ToString() ?? ZVecDefaults.Filter.Null
    };

    private static string FormatString(string value)
        => string.Concat(
            ZVecDefaults.Filter.DoubleQuote,
            EscapeString(value),
            ZVecDefaults.Filter.DoubleQuote);

    private static string EscapeString(string value)
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

    private static string FormatNumber<T>(T value) where T : IFormattable
        => value.ToString(null, CultureInfo.InvariantCulture)!;
}
