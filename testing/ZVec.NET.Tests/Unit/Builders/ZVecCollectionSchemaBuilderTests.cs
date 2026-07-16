using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Builders;

public class ZVecCollectionSchemaBuilderTests
{
    [Fact]
    public void SchemaBuilder_AddField_FluentApi()
    {
        var schema = new ZVecCollectionSchemaBuilder("products")
            .AddField("title", ZVecDataType.String)
            .AddVector("embedding", ZVecDataType.VectorFp32, 768,
                new ZVecHnswIndexParam { MetricType = ZVecMetricType.Cosine })
            .Build();

        schema.Name.Should().Be("products");
        schema.Fields.Should().HaveCount(1);
        schema.Fields[0].Name.Should().Be("title");
        schema.Fields[0].DataType.Should().Be(ZVecDataType.String);
        schema.Vectors.Should().HaveCount(1);
        schema.Vectors[0].Dimension.Should().Be(768);
        schema.Vectors[0].IndexParam.Should().BeOfType<ZVecHnswIndexParam>();
        schema.MaxDocCountPerSegment.Should().Be(ZVecDefaults.Collection.MaxDocCountPerSegment);
    }

    [Fact]
    public void SchemaBuilder_AddField_WithInvertIndex_AndNullable()
    {
        var schema = new ZVecCollectionSchemaBuilder("docs")
            .AddField("category", ZVecDataType.String, nullable: true,
                index: new ZVecInvertIndexParam { EnableRangeOptimization = true })
            .Build();

        schema.Fields.Should().HaveCount(1);
        schema.Fields[0].Nullable.Should().BeTrue();
        schema.Fields[0].IndexParam.Should().BeOfType<ZVecInvertIndexParam>();
        schema.Fields[0].IndexParam!.EnableRangeOptimization.Should().BeTrue();
    }

    [Fact]
    public void SchemaBuilder_AddField_AndAddVector_SchemaOverloads()
    {
        var field = new ZVecFieldSchema { Name = "year", DataType = ZVecDataType.Int32 };
        var vector = new ZVecVectorSchema
        {
            Name = "vec",
            DataType = ZVecDataType.VectorFp32,
            Dimension = 4,
            IndexParam = new ZVecFlatIndexParam()
        };

        var schema = new ZVecCollectionSchemaBuilder("mixed")
            .AddField(field)
            .AddVector(vector)
            .Build();

        schema.Fields.Should().ContainSingle().Which.Name.Should().Be("year");
        schema.Vectors.Should().ContainSingle().Which.IndexParam.Should().BeOfType<ZVecFlatIndexParam>();
    }

    [Fact]
    public void SchemaBuilder_EmptyName_Throws()
    {
        var act = () => new ZVecCollectionSchemaBuilder("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SchemaBuilder_WhitespaceName_Throws()
    {
        var act = () => new ZVecCollectionSchemaBuilder("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SchemaBuilder_InvalidVectorDimension_Throws()
    {
        var builder = new ZVecCollectionSchemaBuilder("test");
        var act = () => builder.AddVector("vec", ZVecDataType.VectorFp32, 0);
        act.Should().Throw<ArgumentException>().WithParameterName("dimension");
    }

    [Fact]
    public void SchemaBuilder_EmptyFieldName_Throws()
    {
        var builder = new ZVecCollectionSchemaBuilder("test");
        var act = () => builder.AddField("", ZVecDataType.String);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SchemaBuilder_EmptyVectorName_Throws()
    {
        var builder = new ZVecCollectionSchemaBuilder("test");
        var act = () => builder.AddVector("", ZVecDataType.VectorFp32, 8);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SchemaBuilder_WithMaxDocCountPerSegment_OverridesDefault()
    {
        var schema = new ZVecCollectionSchemaBuilder("test")
            .WithMaxDocCountPerSegment(5000)
            .Build();

        schema.MaxDocCountPerSegment.Should().Be(5000);
    }

    [Fact]
    public void SchemaBuilder_WithMaxDocCountPerSegment_NonPositive_Throws()
    {
        var builder = new ZVecCollectionSchemaBuilder("test");
        var act = () => builder.WithMaxDocCountPerSegment(0);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void SchemaBuilder_NullFieldOrVector_Throws()
    {
        var builder = new ZVecCollectionSchemaBuilder("test");

        var fieldAct = () => builder.AddField((ZVecFieldSchema)null!);
        fieldAct.Should().Throw<ArgumentNullException>();

        var vectorAct = () => builder.AddVector((ZVecVectorSchema)null!);
        vectorAct.Should().Throw<ArgumentNullException>();
    }
}
