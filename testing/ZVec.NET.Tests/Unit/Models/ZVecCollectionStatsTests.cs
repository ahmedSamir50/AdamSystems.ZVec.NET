using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Models;

public class ZVecCollectionStatsTests
{
    [Fact]
    public void ZVecCollectionStats_Defaults_EmptyCompleteness()
    {
        var stats = new ZVecCollectionStats();
        stats.DocCount.Should().Be(0);
        stats.IndexCompleteness.Should().BeEmpty();
    }

    [Fact]
    public void ZVecCollectionStats_WithValues()
    {
        var stats = new ZVecCollectionStats
        {
            DocCount = 42,
            IndexCompleteness = new Dictionary<string, float> { ["embedding"] = 1.0f }
        };
        stats.DocCount.Should().Be(42);
        stats.IndexCompleteness["embedding"].Should().Be(1.0f);
    }
}
