using FluentAssertions;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// US-E18.9 — version gate: too-old / wrong-major (not exact patch).
/// Logic is covered by unit tests; this verifies the live ABI path when native is available.
/// </summary>
public class VersionGateIntegrationTests : IClassFixture<ZVecRealNativeFixture>
{
    private readonly ZVecRealNativeFixture _fixture;

    public VersionGateIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Initialize_WithMatchingAbi_Succeeds()
    {
        _fixture.SkipIfNotAvailable();

        var previousMajor = ZVecDefaults.Version.ExpectedMajor;
        var previousMinor = ZVecDefaults.Version.ExpectedMinor;
        var previousPatch = ZVecDefaults.Version.ExpectedPatch;
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;

        try
        {
            ZVecDefaults.Version.BypassAbiCheck = false;
            ZVecDefaults.Version.ExpectedMajor = ZVecNativeAbi.MinimumMajor;
            ZVecDefaults.Version.ExpectedMinor = ZVecNativeAbi.MinimumMinor;
            ZVecDefaults.Version.ExpectedPatch = ZVecNativeAbi.MinimumPatch;

            // Factory may already be initialized process-wide; smoke-check version APIs instead
            // when re-init would be a no-op.
            using var factory = new ZVecFactory();
            factory.Initialize();
            factory.IsInitialized.Should().BeTrue();

            ZVec.NET.Interop.NativeMethods.zvec_get_version_major()
                .Should().Be(ZVecNativeAbi.MinimumMajor);
        }
        finally
        {
            ZVecDefaults.Version.ExpectedMajor = previousMajor;
            ZVecDefaults.Version.ExpectedMinor = previousMinor;
            ZVecDefaults.Version.ExpectedPatch = previousPatch;
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void IsCompatible_RejectsTooOldAndWrongMajor()
    {
        // Pure gate semantics used by CheckAbiVersion (too-old / wrong-major).
        ZVecNativeAbi.IsCompatible(meetsMinimumVersion: false, foundMajor: 0, requiredMajor: 0)
            .Should().BeFalse("too-old minimum must fail");

        ZVecNativeAbi.IsCompatible(meetsMinimumVersion: true, foundMajor: 1, requiredMajor: 0)
            .Should().BeFalse("wrong major must fail even if SemVer floor passes");

        ZVecNativeAbi.IsCompatible(meetsMinimumVersion: true, foundMajor: 0, requiredMajor: 0)
            .Should().BeTrue();

        var ex = new ZVecAbiMismatchException("9.0.0", 9, "0.5.1");
        ex.Message.Should().Contain(">= '9.0.0'").And.Contain("major == 9");
    }
}
