using System.Globalization;
using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Unit.Query;

public class ZVecFilterBuilderTests
{
    private static string Cmp(string field, string op, string value)
        => string.Concat(field, ZVecDefaults.Filter.Space, op, ZVecDefaults.Filter.Space, value);

    private static string Quoted(string value)
        => string.Concat(ZVecDefaults.Filter.DoubleQuote, value, ZVecDefaults.Filter.DoubleQuote);

    [Fact]
    public void FilterBuilder_WhereEq_Int_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Eq, 25);

        filter.ToString().Should().Be(Cmp("age", ZVecDefaults.Filter.Eq, "25"));
    }

    [Fact]
    public void FilterBuilder_Where_Long_Float_Double_Bool_Overloads()
    {
        ZVecFilterBuilder.Create().Where("id", ZVecCompareOp.Eq, 100L)
            .ToString().Should().Be(Cmp("id", ZVecDefaults.Filter.Eq, "100"));

        ZVecFilterBuilder.Create().Where("score", ZVecCompareOp.Gt, 1.5f)
            .ToString().Should().Be(Cmp("score", ZVecDefaults.Filter.Gt, 1.5f.ToString(CultureInfo.InvariantCulture)));

        ZVecFilterBuilder.Create().Where("score", ZVecCompareOp.Lt, 2.5d)
            .ToString().Should().Be(Cmp("score", ZVecDefaults.Filter.Lt, 2.5d.ToString(CultureInfo.InvariantCulture)));

        ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true)
            .ToString().Should().Be(Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.True));

        ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, false)
            .ToString().Should().Be(Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.False));
    }

    [Fact]
    public void FilterBuilder_Where_AllCompareOps()
    {
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Ne, 1)
            .ToString().Should().Be(Cmp("a", ZVecDefaults.Filter.Ne, "1"));
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Ge, 1)
            .ToString().Should().Be(Cmp("a", ZVecDefaults.Filter.Ge, "1"));
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Le, 1)
            .ToString().Should().Be(Cmp("a", ZVecDefaults.Filter.Le, "1"));
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Like, "x%")
            .ToString().Should().Be(Cmp("a", ZVecDefaults.Filter.Like, Quoted("x%")));
    }

    [Fact]
    public void FilterBuilder_CompoundAnd_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .And(ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true));

        var expected = string.Concat(
            Cmp("age", ZVecDefaults.Filter.Gt, "18"),
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.And,
            ZVecDefaults.Filter.Space,
            Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.True));

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Or_ParenthesizesBothSides()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Lt, 10)
            .Or(ZVecFilterBuilder.Create().Where("age", ZVecCompareOp.Gt, 60));

        var expected = string.Concat(
            ZVecDefaults.Filter.OpenParen,
            Cmp("age", ZVecDefaults.Filter.Lt, "10"),
            ZVecDefaults.Filter.CloseParen,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Or,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            Cmp("age", ZVecDefaults.Filter.Gt, "60"),
            ZVecDefaults.Filter.CloseParen);

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Not_RewritesEqualityToInequality()
    {
        var inner = ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, false);
        var filter = ZVecFilterBuilder.Create().Not(inner);

        filter.ToString().Should().Be(Cmp("active", ZVecDefaults.Filter.Ne, ZVecDefaults.Filter.False));
    }

    [Fact]
    public void FilterBuilder_Not_RewritesInToNotIn()
    {
        var inner = ZVecFilterBuilder.Create().In("status", "open");
        var filter = ZVecFilterBuilder.Create().Not(inner);

        var expected = string.Concat(
            "status",
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Not,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.In,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            Quoted("open"),
            ZVecDefaults.Filter.CloseParen);

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Not_Compound_Throws()
    {
        var compound = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .And(ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true));

        var act = () => ZVecFilterBuilder.Create().Not(compound);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FilterBuilder_In_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create().In("status", "open", "closed");

        var expected = string.Concat(
            "status",
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.In,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            Quoted("open"),
            ZVecDefaults.Filter.CommaSpace,
            Quoted("closed"),
            ZVecDefaults.Filter.CloseParen);

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Like_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create().Like("name", "Al%");

        var expected = string.Concat(
            "name",
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.Like,
            ZVecDefaults.Filter.Space,
            Quoted("Al%"));

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_ContainAny_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create()
            .ContainAny("tags", "sport", "music");

        var expected = string.Concat(
            "tags",
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAny,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            Quoted("sport"),
            ZVecDefaults.Filter.CommaSpace,
            Quoted("music"),
            ZVecDefaults.Filter.CloseParen);

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_ContainAll_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create()
            .ContainAll("tags", "a", "b");

        var expected = string.Concat(
            "tags",
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.ContainAll,
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.OpenParen,
            Quoted("a"),
            ZVecDefaults.Filter.CommaSpace,
            Quoted("b"),
            ZVecDefaults.Filter.CloseParen);

        filter.ToString().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_StringWithQuote_EscapesCorrectly()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("name", ZVecCompareOp.Eq, "O'Brien");

        var escaped = string.Concat(
            "O",
            ZVecDefaults.Filter.Backslash,
            ZVecDefaults.Filter.SingleQuote,
            "Brien");

        filter.ToString().Should().Be(Cmp("name", ZVecDefaults.Filter.Eq, Quoted(escaped)));
    }

    [Fact]
    public void FilterBuilder_StringWithBackslashAndDoubleQuote_EscapesCorrectly()
    {
        var input = string.Concat("a", ZVecDefaults.Filter.Backslash, "b", ZVecDefaults.Filter.DoubleQuote, "c");
        var filter = ZVecFilterBuilder.Create().Where("name", ZVecCompareOp.Eq, input);

        var escaped = string.Concat(
            "a",
            ZVecDefaults.Filter.Backslash, ZVecDefaults.Filter.Backslash,
            "b",
            ZVecDefaults.Filter.Backslash, ZVecDefaults.Filter.DoubleQuote,
            "c");

        filter.ToString().Should().Be(Cmp("name", ZVecDefaults.Filter.Eq, Quoted(escaped)));
    }

    [Fact]
    public void FilterBuilder_EmptyFieldName_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("", ZVecCompareOp.Eq, 1);
        act.Should().Throw<ArgumentException>().WithParameterName("fieldName");
    }

    [Fact]
    public void FilterBuilder_UnsupportedCompareOp_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("age", ZVecCompareOp.IsNull, 1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("op");
    }

    [Fact]
    public void FilterBuilder_NullInner_AndOrNot_Throws()
    {
        var builder = ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1);

        var andAct = () => builder.And(null!);
        andAct.Should().Throw<ArgumentNullException>().WithParameterName("inner");

        var orAct = () => builder.Or(null!);
        orAct.Should().Throw<ArgumentNullException>().WithParameterName("inner");

        var notAct = () => ZVecFilterBuilder.Create().Not(null!);
        notAct.Should().Throw<ArgumentNullException>().WithParameterName("inner");
    }

    [Fact]
    public void FilterBuilder_NullStringValue_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("name", ZVecCompareOp.Eq, (string)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterBuilder_In_NullOrEmptyValues_Throws()
    {
        var nullAct = () => ZVecFilterBuilder.Create().In("status", null!);
        nullAct.Should().Throw<ArgumentNullException>();

        var emptyAct = () => ZVecFilterBuilder.Create().In("status");
        emptyAct.Should().Throw<ArgumentException>().WithParameterName("values");
    }

    [Fact]
    public void FilterBuilder_Like_NullPattern_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Like("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
