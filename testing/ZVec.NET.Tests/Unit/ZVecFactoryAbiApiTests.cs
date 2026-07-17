using FluentAssertions;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Unit;

/// <summary>US-E10 — factory version/ABI surface without requiring native round-trips.</summary>
public class ZVecFactoryAbiApiTests
{
    [Fact]
    public void GetAbiInfo_AlwaysReportsRequiredAbi()
    {
        var factory = new ZVecFactory();

        var abi = factory.GetAbiInfo();
        abi.RequiredMinimumVersion.Should().Be(ZVecNativeAbi.MinimumVersionString);
        abi.RequiredMajor.Should().Be(ZVecNativeAbi.MinimumMajor);

        if (NativeLibraryResolver.IsLoaded)
        {
            abi.FoundVersion.Should().NotBeNullOrWhiteSpace();
            abi.FoundMajor.Should().NotBeNull();
        }
        else
        {
            abi.FoundVersion.Should().BeNull();
            abi.FoundMajor.Should().BeNull();
            abi.IsCompatible.Should().BeFalse();
        }
    }

    [Fact]
    public void GetNativeVersion_WhenNotInitialized_ThrowsInvalidOperationException()
    {
        var factory = new ZVecFactory();

        var act = () => factory.GetNativeVersion();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{ZVecDefaults.Errors.FactoryNotInitialized}*");
    }
}
