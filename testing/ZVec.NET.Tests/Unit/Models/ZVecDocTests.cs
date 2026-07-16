using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Models;

public class ZVecDocTests
{
    [Fact]
    public void ZVecDoc_Create_WithRequiredId_Succeeds()
    {
        var doc = ZVecDoc.Create("doc1");
        doc.Id.Should().Be("doc1");
        doc.DenseVectors.Should().BeEmpty();
        doc.SparseVectors.Should().BeEmpty();
        doc.Fields.Should().BeEmpty();
        doc.Score.Should().Be(0f);
    }

    [Fact]
    public void ZVecDoc_Create_NullId_Throws()
    {
        var act = () => ZVecDoc.Create(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ZVecDoc_Create_WhitespaceId_Throws()
    {
        var act = () => ZVecDoc.Create("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ZVecDoc_Create_WithDenseVectors_NoCopy()
    {
        var vec = new float[128];
        var mem = new ReadOnlyMemory<float>(vec);
        var doc = ZVecDoc.Create("d1", denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["v"] = mem });
        doc.DenseVectors["v"].Span[0].Should().Be(0f);
        // Same underlying array â€” no copy
        doc.DenseVectors["v"].Equals(mem).Should().BeTrue();
    }
}
