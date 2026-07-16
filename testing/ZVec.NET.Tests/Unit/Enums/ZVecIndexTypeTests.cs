using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Enums;

public class ZVecIndexTypeTests
{
    [Fact]
    public void ZVecIndexType_HasCorrectValues()
    {
        ((int)ZVecIndexType.Undefined).Should().Be(0);
        ((int)ZVecIndexType.Hnsw).Should().Be(1);
        ((int)ZVecIndexType.Ivf).Should().Be(2);
        ((int)ZVecIndexType.Flat).Should().Be(3);
        ((int)ZVecIndexType.HnswRabitq).Should().Be(4);
        ((int)ZVecIndexType.DiskAnn).Should().Be(5);
        ((int)ZVecIndexType.Vamana).Should().Be(6);
        ((int)ZVecIndexType.Invert).Should().Be(10);
        ((int)ZVecIndexType.Fts).Should().Be(11);
    }
}
