using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Models;

public class ZVecStatusTests
{
    [Fact]
    public void ZVecStatus_Ok_IsSuccess()
    {
        var status = new ZVecStatus { Code = ZVecErrorCode.Ok };
        status.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ZVecStatus_NotOk_IsNotSuccess()
    {
        var status = new ZVecStatus { Code = ZVecErrorCode.NotFound, Message = "missing" };
        status.IsSuccess.Should().BeFalse();
        status.Message.Should().Be("missing");
    }
}
