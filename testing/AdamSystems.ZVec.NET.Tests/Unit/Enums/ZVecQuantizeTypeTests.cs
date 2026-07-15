using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Enums;

public class ZVecQuantizeTypeTests
{
    [Fact]
    public void ZVecQuantizeType_HasCorrectValues()
    {
        ((int)ZVecQuantizeType.Undefined).Should().Be(0);
        ((int)ZVecQuantizeType.Fp16).Should().Be(1);
        ((int)ZVecQuantizeType.Int8).Should().Be(2);
        ((int)ZVecQuantizeType.Int4).Should().Be(3);
        ((int)ZVecQuantizeType.Rabitq).Should().Be(4);
    }
}
