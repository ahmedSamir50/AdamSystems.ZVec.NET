using FluentAssertions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.Mapping;

public class ZVecMapperTests
{
    [Fact]
    public void ToDoc_And_FromDoc_RoundTrip()
    {
        var product = new Product
        {
            Id = "p1",
            Title = "Book",
            Category = "fiction",
            Embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
        };

        var doc = ZVecMapper.ToDoc(product);
        doc.Id.Should().Be("p1");
        doc.Fields["Title"].Should().Be("Book");
        doc.Fields["Category"].Should().Be("fiction");
        doc.DenseVectors["Embedding"].ToArray().Should().Equal(0.1f, 0.2f, 0.3f, 0.4f);

        var back = ZVecMapper.FromDoc<Product>(doc);
        back.Id.Should().Be("p1");
        back.Title.Should().Be("Book");
        back.Category.Should().Be("fiction");
        back.Embedding.ToArray().Should().Equal(0.1f, 0.2f, 0.3f, 0.4f);
    }

    [Fact]
    public void FromDoc_IgnoresUnknownNativeFields()
    {
        var doc = ZVecDoc.Create(
            "p1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["Embedding"] = new float[] { 1, 2, 3, 4 }
            },
            fields: new Dictionary<string, object>
            {
                ["Title"] = "X",
                ["Category"] = "Y",
                ["leftover"] = "ignored"
            });

        var product = ZVecMapper.FromDoc<Product>(doc);
        product.Title.Should().Be("X");
    }

    [Fact]
    public void FromDoc_UsesStorageNameOverride()
    {
        var doc = ZVecDoc.Create(
            "p1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["emb"] = new float[] { 1, 2, 3, 4 }
            },
            fields: new Dictionary<string, object> { ["cat"] = "sci" });

        var product = ZVecMapper.FromDoc<NamedProduct>(doc);
        product.Category.Should().Be("sci");
        product.Embedding.ToArray().Should().Equal(1, 2, 3, 4);
    }

    private sealed class Product
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    private sealed class NamedProduct
    {
        public string Id { get; set; } = "";
        [ZVecField("cat")]
        public string Category { get; set; } = "";
        [ZVecVector("emb", 4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
