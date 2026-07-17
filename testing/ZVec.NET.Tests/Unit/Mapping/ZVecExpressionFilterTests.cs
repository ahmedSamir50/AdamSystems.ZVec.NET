using FluentAssertions;
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
