using FluentAssertions;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.Mapping;

public class ZVecExpressionFilterTests
{
    [Fact]
    public void Translate_EqualityAndRelational()
    {
        const string category = "fiction";
        var filter = ZVecExpressionFilter.Translate<Product>(p => p.Category == category && p.Year > 2020);
        filter.Should().Be("Category = \"fiction\" AND Year > 2020");
    }

    [Fact]
    public void Translate_OrAndNotNull()
    {
        var filter = ZVecExpressionFilter.Translate<Product>(p => p.Title != null || p.Year >= 2000);
        filter.Should().Contain("IS NOT NULL").And.Contain("Year >= 2000").And.Contain(" OR ");
    }

    [Fact]
    public void Translate_UsesStorageNameOverride()
    {
        var filter = ZVecExpressionFilter.Translate<NamedProduct>(p => p.Category == "ai");
        filter.Should().Be("cat = \"ai\"");
    }

    [Fact]
    public void Translate_Not_AndNested()
    {
        var filter = ZVecExpressionFilter.Translate<Product>(p => !(p.Year < 2000) && (p.Category == "a" || p.Category == "b"));
        filter.Should().Contain("Year >=").And.Contain(" OR ").And.Contain("Category = \"a\"");
    }

    [Fact]
    public void Translate_InvertedRelational_ConstantOnLeft()
    {
        var filter = ZVecExpressionFilter.Translate<Product>(p => 5 < p.Year);
        filter.Should().Be("Year > 5");
    }

    [Fact]
    public void Translate_Unsupported_Throws()
    {
        var act = () => ZVecExpressionFilter.Translate<Product>(p => p.Category.StartsWith("x"));
        act.Should().Throw<ZVecException>().WithMessage("*Unsupported filter expression*");
    }

    private sealed class Product
    {
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        public string Category { get; set; } = "";
        public int Year { get; set; }
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    private sealed class NamedProduct
    {
        public string Id { get; set; } = "";
        [ZVecField("cat")]
        public string Category { get; set; } = "";
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
