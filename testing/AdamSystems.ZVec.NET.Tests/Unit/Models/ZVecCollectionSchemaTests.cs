using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Models;

public class ZVecCollectionSchemaTests
{
    [Fact]
    public void ZVecCollectionSchema_DefaultMaxDocCount()
    {
        var schema = new ZVecCollectionSchema { Name = "test", Fields = [], Vectors = [] };
        schema.MaxDocCountPerSegment.Should().Be(ZVecDefaults.Collection.MaxDocCountPerSegment);
        schema.Fields.Should().BeEmpty();
        schema.Vectors.Should().BeEmpty();
    }

    [Fact]
    public void ZVecFieldSchema_WithInvertIndex()
    {
        var field = new ZVecFieldSchema
        {
            Name = "title",
            DataType = ZVecDataType.String,
            Nullable = true,
            IndexParam = new ZVecInvertIndexParam { EnableRangeOptimization = true }
        };
        field.Name.Should().Be("title");
        field.Nullable.Should().BeTrue();
        field.IndexParam!.EnableRangeOptimization.Should().BeTrue();
    }

    [Fact]
    public void ZVecVectorSchema_WithHnswIndex()
    {
        var vector = new ZVecVectorSchema
        {
            Name = "embedding",
            DataType = ZVecDataType.VectorFp32,
            Dimension = 128,
            IndexParam = new ZVecHnswIndexParam()
        };
        vector.Dimension.Should().Be(128);
        vector.IndexParam.Should().BeOfType<ZVecHnswIndexParam>();
    }
}
