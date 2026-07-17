using FluentAssertions;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Unit.Interop;

public class NativeLibraryResolverRidTests
{
    [Theory]
    [InlineData("win-x64", "win-x64")]
    [InlineData("win10-x64", "win-x64")]
    [InlineData("win-arm64", "win-arm64")]
    [InlineData("linux-x64", "linux-x64")]
    [InlineData("ubuntu.22.04-x64", "linux-x64")]
    [InlineData("linux-arm64", "linux-arm64")]
    [InlineData("osx-arm64", "osx-arm64")]
    [InlineData("osx-x64", "osx-x64")]
    [InlineData("android-arm64", "android-arm64")]
    [InlineData("android-x64", "android-x64")]
    [InlineData("ios-arm64", "ios-arm64")]
    [InlineData("iossimulator-arm64", "iossimulator-arm64")]
    [InlineData("maccatalyst-arm64", "maccatalyst-arm64")]
    public void GetPortableRid_NormalizesKnownRids(string input, string expected)
    {
        NativeLibraryResolver.GetPortableRid(input).Should().Be(expected);
    }

    [Fact]
    public void GetNativeLibraryFileName_MatchesOsFamily()
    {
        string name = NativeLibraryResolver.GetNativeLibraryFileName();
        if (OperatingSystem.IsWindows())
            name.Should().Be("zvec_c_api.dll");
        else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            name.Should().Be("libzvec_c_api.dylib");
        else
            name.Should().Be("libzvec_c_api.so");
    }
}
