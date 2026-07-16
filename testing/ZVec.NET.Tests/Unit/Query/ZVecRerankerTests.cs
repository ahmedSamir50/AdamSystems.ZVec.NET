using FluentAssertions;

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
}
