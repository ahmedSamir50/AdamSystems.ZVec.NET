using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Enums;

public class ZVecMetricTypeTests
{
    [Fact]
    public void ZVecMetricType_HasCorrectValues()
    {
        ((int)ZVecMetricType.Undefined).Should().Be(0);
        ((int)ZVecMetricType.L2).Should().Be(1);
        ((int)ZVecMetricType.Ip).Should().Be(2);
        ((int)ZVecMetricType.Cosine).Should().Be(3);
        ((int)ZVecMetricType.MipsL2).Should().Be(4);
    }
}
