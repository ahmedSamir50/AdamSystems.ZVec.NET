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

        filter.Build().Should().Be(Cmp("age", ZVecDefaults.Filter.Eq, "25"));
    }

    [Fact]
    public void FilterBuilder_Where_Long_Float_Double_Bool_Overloads()
    {
        ZVecFilterBuilder.Create().Where("id", ZVecCompareOp.Eq, 100L)
            .Build().Should().Be(Cmp("id", ZVecDefaults.Filter.Eq, "100"));

        ZVecFilterBuilder.Create().Where("score", ZVecCompareOp.Gt, 1.5f)
            .Build().Should().Be(Cmp("score", ZVecDefaults.Filter.Gt, 1.5f.ToString(CultureInfo.InvariantCulture)));

        ZVecFilterBuilder.Create().Where("score", ZVecCompareOp.Lt, 2.5d)
            .Build().Should().Be(Cmp("score", ZVecDefaults.Filter.Lt, 2.5d.ToString(CultureInfo.InvariantCulture)));

        ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, true)
            .Build().Should().Be(Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.True));

        ZVecFilterBuilder.Create().Where("active", ZVecCompareOp.Eq, false)
            .Build().Should().Be(Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.False));
    }

    [Fact]
    public void FilterBuilder_Where_AllRelationalOps()
    {
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Ne, 1)
            .Build().Should().Be(Cmp("a", ZVecDefaults.Filter.Ne, "1"));
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Ge, 1)
            .Build().Should().Be(Cmp("a", ZVecDefaults.Filter.Ge, "1"));
        ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Le, 1)
            .Build().Should().Be(Cmp("a", ZVecDefaults.Filter.Le, "1"));
    }

    [Fact]
    public void FilterBuilder_Where_ContainAnyOp_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("tags", ZVecCompareOp.ContainAny, "AI");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("op");
    }

    [Fact]
    public void FilterBuilder_Where_LikeOp_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("name", ZVecCompareOp.Like, "x%");
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("op");
    }

    [Fact]
    public void FilterBuilder_ChainedWhere_AndsTogether()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .Where("active", ZVecCompareOp.Eq, true);

        var expected = string.Concat(
            Cmp("age", ZVecDefaults.Filter.Gt, "18"),
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.And,
            ZVecDefaults.Filter.Space,
            Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.True));

        filter.Build().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_CompoundAnd_Lambda_GeneratesCorrectString()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Gt, 18)
            .And(f => f.Where("active", ZVecCompareOp.Eq, true));

        var expected = string.Concat(
            Cmp("age", ZVecDefaults.Filter.Gt, "18"),
            ZVecDefaults.Filter.Space,
            ZVecDefaults.Filter.And,
            ZVecDefaults.Filter.Space,
            Cmp("active", ZVecDefaults.Filter.Eq, ZVecDefaults.Filter.True));

        filter.Build().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Or_Lambda_ParenthesizesBothSides()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("age", ZVecCompareOp.Lt, 10)
            .Or(f => f.Where("age", ZVecCompareOp.Gt, 60));

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

        filter.Build().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_NestedLambda_MatchesReadmeShape()
    {
        var filter = ZVecFilterBuilder.Create()
            .Where("publish_year", ZVecCompareOp.Gt, 2020)
            .And(f => f
                .Where("category", ZVecCompareOp.Eq, "fiction")
                .Or(g => g.ContainAny("tags", "AI", "ML")));

        filter.Build().Should().Contain(ZVecDefaults.Filter.ContainAny);
        filter.Build().Should().Contain(ZVecDefaults.Filter.OpenParen);
        filter.Build().Should().NotContain(ZVecDefaults.Filter.OpenBracket);
    }

    [Fact]
    public void FilterBuilder_Not_RewritesEqualityToInequality()
    {
        var filter = ZVecFilterBuilder.Create()
            .Not(f => f.Where("active", ZVecCompareOp.Eq, false));

        filter.Build().Should().Be(Cmp("active", ZVecDefaults.Filter.Ne, ZVecDefaults.Filter.False));
    }

    [Fact]
    public void FilterBuilder_Not_ValueContainingAnd_Succeeds()
    {
        var filter = ZVecFilterBuilder.Create()
            .Not(f => f.Where("description", ZVecCompareOp.Eq, "This AND that"));

        var escaped = string.Concat(
            "This ",
            ZVecDefaults.Filter.And,
            " that"); // value text — escaping only quotes/backslash; AND inside value is fine
        // Actual value is "This AND that" with AND as content
        escaped = "This AND that";
        var expected = Cmp(
            "description",
            ZVecDefaults.Filter.Ne,
            Quoted(escaped));

        filter.Build().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Not_RewritesInToNotIn()
    {
        var filter = ZVecFilterBuilder.Create().Not(f => f.In("status", "open"));

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

        filter.Build().Should().Be(expected);
    }

    [Fact]
    public void FilterBuilder_Not_Compound_UsesDeMorgan()
    {
        var filter = ZVecFilterBuilder.Create()
            .Not(f => f
                .Where("age", ZVecCompareOp.Gt, 18)
                .And(g => g.Where("active", ZVecCompareOp.Eq, true)));

        // NOT (age > 18 AND active = true) => (age <= 18) OR (active != true)
        filter.Build().Should().Contain(ZVecDefaults.Filter.Or);
        filter.Build().Should().Contain(ZVecDefaults.Filter.Le);
        filter.Build().Should().Contain(ZVecDefaults.Filter.Ne);
    }

    [Fact]
    public void FilterBuilder_And_EmptyLeft_AbsorbsRight()
    {
        var right = ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1);
        ZVecFilterBuilder.Create().And(right).Build().Should().Be(right.Build());
    }

    [Fact]
    public void FilterBuilder_And_EmptyRight_Throws()
    {
        var act = () => ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1).And(ZVecFilterBuilder.Create());
        act.Should().Throw<ArgumentException>().WithMessage($"*{ZVecDefaults.Errors.FilterEmptyRightOperand}*");
    }

    [Fact]
    public void FilterBuilder_In_Like_ContainAny_ContainAll()
    {
        ZVecFilterBuilder.Create().In("status", "open", "closed").Build()
            .Should().Contain(ZVecDefaults.Filter.In);

        ZVecFilterBuilder.Create().Like("name", "Al%").Build()
            .Should().Be(Cmp("name", ZVecDefaults.Filter.Like, Quoted("Al%")));

        var any = ZVecFilterBuilder.Create().ContainAny("tags", "sport", "music").Build();
        any.Should().StartWith("tags");
        any.Should().Contain(ZVecDefaults.Filter.ContainAny);
        any.Should().Contain(ZVecDefaults.Filter.OpenParen);
        any.Should().NotContain(ZVecDefaults.Filter.OpenBracket);

        ZVecFilterBuilder.Create().ContainAll("tags", "a", "b").Build()
            .Should().Contain(ZVecDefaults.Filter.ContainAll);
    }

    [Fact]
    public void FilterBuilder_IsNull_IsNotNull()
    {
        ZVecFilterBuilder.Create().IsNull("title").Build()
            .Should().Be(string.Concat("title", ZVecDefaults.Filter.Space, ZVecDefaults.Filter.IsNull));

        ZVecFilterBuilder.Create().IsNotNull("title").Build()
            .Should().Be(string.Concat("title", ZVecDefaults.Filter.Space, ZVecDefaults.Filter.IsNotNull));

        ZVecFilterBuilder.Create().Not(f => f.IsNull("title")).Build()
            .Should().Be(string.Concat("title", ZVecDefaults.Filter.Space, ZVecDefaults.Filter.IsNotNull));
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

        filter.Build().Should().Be(Cmp("name", ZVecDefaults.Filter.Eq, Quoted(escaped)));
    }

    [Fact]
    public void FilterBuilder_InvalidFieldName_Throws()
    {
        var empty = () => ZVecFilterBuilder.Create().Where("", ZVecCompareOp.Eq, 1);
        empty.Should().Throw<ArgumentException>().WithParameterName("fieldName");

        var spaces = () => ZVecFilterBuilder.Create().Where("a b", ZVecCompareOp.Eq, 1);
        spaces.Should().Throw<ArgumentException>().WithParameterName("fieldName");

        var inject = () => ZVecFilterBuilder.Create().Where("x;DROP", ZVecCompareOp.Eq, 1);
        inject.Should().Throw<ArgumentException>().WithParameterName("fieldName");
    }

    [Fact]
    public void FilterBuilder_NullInner_AndOrNot_Throws()
    {
        var builder = ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1);

        var andAct = () => builder.And((ZVecFilterBuilder)null!);
        andAct.Should().Throw<ArgumentNullException>();

        var orAct = () => builder.Or((Func<ZVecFilterBuilder, ZVecFilterBuilder>)null!);
        orAct.Should().Throw<ArgumentNullException>();

        var notAct = () => ZVecFilterBuilder.Create().Not((ZVecFilterBuilder)null!);
        notAct.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FilterBuilder_ToString_IncludesPrefix()
    {
        var filter = ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1);
        filter.ToString().Should().StartWith(ZVecDefaults.Filter.BuilderToStringPrefix);
        filter.ToString().Should().Contain(filter.Build());
    }

    [Fact]
    public void FilterBuilder_Immutability_AndDoesNotMutateOriginal()
    {
        var left = ZVecFilterBuilder.Create().Where("a", ZVecCompareOp.Eq, 1);
        var original = left.Build();
        _ = left.And(f => f.Where("b", ZVecCompareOp.Eq, 2));
        left.Build().Should().Be(original);
    }

    [Fact]
    public void FilterBuilder_Create_StartsEmptyBuilder()
    {
        ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020)
            .Build()
            .Should().Be(Cmp("year", ZVecDefaults.Filter.Gt, "2020"));
    }
}
