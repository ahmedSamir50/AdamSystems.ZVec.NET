using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Enums;

public class ZVecErrorCodeTests
{
    [Fact]
    public void ZVecErrorCode_HasCorrectValues()
    {
        ((int)ZVecErrorCode.Ok).Should().Be(0);
        ((int)ZVecErrorCode.NotFound).Should().Be(1);
        ((int)ZVecErrorCode.AlreadyExists).Should().Be(2);
        ((int)ZVecErrorCode.InvalidArgument).Should().Be(3);
        ((int)ZVecErrorCode.PermissionDenied).Should().Be(4);
        ((int)ZVecErrorCode.FailedPrecondition).Should().Be(5);
        ((int)ZVecErrorCode.ResourceExhausted).Should().Be(6);
        ((int)ZVecErrorCode.Unavailable).Should().Be(7);
        ((int)ZVecErrorCode.InternalError).Should().Be(8);
        ((int)ZVecErrorCode.NotSupported).Should().Be(9);
        ((int)ZVecErrorCode.Unknown).Should().Be(10);
    }
}
