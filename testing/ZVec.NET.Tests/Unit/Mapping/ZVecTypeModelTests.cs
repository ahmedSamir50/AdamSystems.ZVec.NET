using FluentAssertions;
using ZVec.NET.Exceptions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.Mapping;

public class ZVecTypeModelTests
{
    [Fact]
    public void Get_UsesConventionAndAttributes()
    {
        var model = ZVecTypeModel.Get<SampleProduct>();

        model.CollectionName.Should().Be("catalog_products");
        model.Id.Property.Name.Should().Be("Id");
        model.Fields.Should().ContainSingle(f => f.StorageName == "category" && f.Property.Name == "Category");
        model.Fields.Should().Contain(f => f.StorageName == "Title");
        model.Vectors.Should().ContainSingle(v => v.StorageName == "embedding" && v.Dimension == 4);
        model.Vectors[0].IndexParam.Should().BeOfType<ZVecHnswIndexParam>();
        ((ZVecHnswIndexParam)model.Vectors[0].IndexParam!).MetricType.Should().Be(ZVecMetricType.Cosine);
    }

    [Fact]
    public void Get_MissingId_Throws()
    {
        var act = () => ZVecTypeModel.Get<NoIdDoc>();
        act.Should().Throw<ZVecException>().WithMessage("*identity*");
    }

    [Fact]
    public void Get_DuplicateId_Throws()
    {
        var act = () => ZVecTypeModel.Get<DuplicateIdDoc>();
        act.Should().Throw<ZVecException>().WithMessage("*more than one identity*");
    }

    [Fact]
    public void Get_VectorWithoutAttribute_Throws()
    {
        var act = () => ZVecTypeModel.Get<VectorMissingAttrDoc>();
        act.Should().Throw<ZVecException>().WithMessage("*Dimension*");
    }

    [ZVecCollection("catalog_products")]
    private sealed class SampleProduct
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        [ZVecField("category")]
        public string Category { get; set; } = "";
        [ZVecIgnore]
        public string Scratch { get; set; } = "";
        [ZVecVector("embedding", 4, Metric = ZVecMetricType.Cosine)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    private sealed class NoIdDoc
    {
        public string Name { get; set; } = "";
    }

    private sealed class DuplicateIdDoc
    {
        [ZVecId]
        public string Key { get; set; } = "";
        public string Id { get; set; } = "";
    }

    private sealed class VectorMissingAttrDoc
    {
        public string Id { get; set; } = "";
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
