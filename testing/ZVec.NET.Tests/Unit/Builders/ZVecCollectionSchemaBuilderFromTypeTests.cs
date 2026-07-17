using FluentAssertions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.Builders;

public class ZVecCollectionSchemaBuilderFromTypeTests
{
    [Fact]
    public void From_BuildsSchemaFromType()
    {
        var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();

        schema.Name.Should().Be("Product");
        schema.Fields.Select(f => f.Name).Should().BeEquivalentTo("Title", "Category");
        schema.Vectors.Should().ContainSingle();
        schema.Vectors[0].Name.Should().Be("Embedding");
        schema.Vectors[0].Dimension.Should().Be(8);
        schema.Vectors[0].IndexParam.Should().BeOfType<ZVecHnswIndexParam>();
    }

    [Fact]
    public void From_RespectsCollectionNameAttribute()
    {
        var schema = ZVecCollectionSchemaBuilder.From<Named>().Build();
        schema.Name.Should().Be("goods");
    }

    private sealed class Product
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        [ZVecVector(8, Metric = ZVecMetricType.L2, M = 16, EfConstruction = 100)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    [ZVecCollection("goods")]
    private sealed class Named
    {
        public string Id { get; set; } = "";
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
