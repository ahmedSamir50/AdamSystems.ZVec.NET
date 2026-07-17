using FluentAssertions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Unit.Query;

public class ZVecRerankerTests
{
    [Fact]
    public void ZVecRrfReranker_DefaultRankConstant()
    {
        var r = new ZVecRrfReranker { TopN = 10 };
        r.RankConstant.Should().Be(ZVecDefaults.Rerank.RankConstant);
        r.TopN.Should().Be(10);
    }

    [Fact]
    public void ZVecWeightedReranker_AcceptsWeights()
    {
        var r = new ZVecWeightedReranker
        {
            TopN = 5,
            Metric = ZVecMetricType.Cosine,
            Weights = new Dictionary<string, float> { ["embedding"] = 0.7f, ["title"] = 0.3f }
        };
        r.Weights.Should().HaveCount(2);
        r.Weights["embedding"].Should().Be(0.7f);
        r.Metric.Should().Be(ZVecMetricType.Cosine);
    }

    [Fact]
    public void ValidateWeights_WithMatchingCount_Succeeds()
    {
        var weights = new Dictionary<string, float>
        {
            ["a"] = 0.5f,
            ["b"] = 0.5f
        };

        var act = () => ZVecWeightedReranker.ValidateWeights(weights, subQueryCount: 2);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateWeights_WithNull_ThrowsArgumentNullException()
    {
        var act = () => ZVecWeightedReranker.ValidateWeights(null!, subQueryCount: 1);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateWeights_WithEmpty_ThrowsArgumentException()
    {
        var act = () => ZVecWeightedReranker.ValidateWeights(new Dictionary<string, float>(), subQueryCount: 1);
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{ZVecDefaults.Errors.RerankerWeightsInvalid}*");
    }

    [Fact]
    public void ValidateWeights_WithMismatchedCount_ThrowsArgumentException()
    {
        var weights = new Dictionary<string, float> { ["a"] = 1f };

        var act = () => ZVecWeightedReranker.ValidateWeights(weights, subQueryCount: 2);
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{ZVecDefaults.Errors.RerankerWeightsInvalid}*");
    }
}
