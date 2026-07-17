using FluentAssertions;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Tests.Unit;

public class ZVecNativeAbiTests
{
    [Fact]
    public void MinimumVersionString_MatchesComponentConstants()
    {
        ZVecNativeAbi.MinimumVersionString.Should().Be(
            $"{ZVecNativeAbi.MinimumMajor}.{ZVecNativeAbi.MinimumMinor}.{ZVecNativeAbi.MinimumPatch}");
    }

    [Theory]
    [InlineData(true, 0, 0, true)]
    [InlineData(true, 1, 0, false)]
    [InlineData(false, 0, 0, false)]
    [InlineData(true, 0, 1, false)]
    public void IsCompatible_RequiresMinimumAndSameMajor(
        bool meetsMinimum,
        int foundMajor,
        int requiredMajor,
        bool expected)
    {
        ZVecNativeAbi.IsCompatible(meetsMinimum, foundMajor, requiredMajor).Should().Be(expected);
    }

    [Fact]
    public void AbiMismatchException_Message_RequiresMinAndSameMajor()
    {
        var ex = new ZVecAbiMismatchException(
            expectedMinimum: ZVecNativeAbi.MinimumVersionString,
            requiredMajor: ZVecNativeAbi.MinimumMajor,
            found: "0.4.0");

        ex.Message.Should().Contain($">= '{ZVecNativeAbi.MinimumVersionString}'");
        ex.Message.Should().Contain($"major == {ZVecNativeAbi.MinimumMajor}");
        ex.Message.Should().Contain("0.4.0");
    }
}
