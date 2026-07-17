using System.Runtime.InteropServices;
using FluentAssertions;
using ZVec.NET.Internal;

namespace ZVec.NET.Tests.Unit.Internal;

public class ZVecPlatformRequirementsTests
{
    [Fact]
    public void ThrowIfUnsupported_Hnsw_DoesNotThrow()
    {
        var act = () => ZVecPlatformRequirements.ThrowIfUnsupported(new ZVecHnswIndexParam());
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfUnsupported_Rabitq_ThrowsOnArm()
    {
        if (RuntimeInformation.ProcessArchitecture is not (Architecture.Arm or Architecture.Arm64))
        {
            Assert.Skip("Current process is not Arm/Arm64; RaBitQ ARM gate cannot be asserted here.");
        }

        var act = () => ZVecPlatformRequirements.ThrowIfUnsupported(new ZVecHnswRabitqIndexParam());
        act.Should().Throw<PlatformNotSupportedException>()
            .WithMessage(ZVecDefaults.Errors.RabitqRequiresX64Avx2);
    }

    [Fact]
    public void ThrowIfUnsupported_Rabitq_AllowsX64()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
        {
            Assert.Skip("Current process is Arm/Arm64; RaBitQ x64 allowance cannot be asserted here.");
        }

        var act = () => ZVecPlatformRequirements.ThrowIfUnsupported(new ZVecHnswRabitqIndexParam());
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfUnsupported_DiskAnn_ThrowsOnNonLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            Assert.Skip("Current OS is Linux; DiskANN non-Linux gate cannot be asserted here.");
        }

        var act = () => ZVecPlatformRequirements.ThrowIfUnsupported(new ZVecDiskAnnIndexParam());
        act.Should().Throw<PlatformNotSupportedException>()
            .WithMessage(ZVecDefaults.Errors.DiskAnnRequiresLinuxLibaio);
    }

    [Fact]
    public void ThrowIfUnsupported_DiskAnn_AllowsLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("Current OS is not Linux; DiskANN Linux allowance cannot be asserted here.");
        }

        var act = () => ZVecPlatformRequirements.ThrowIfUnsupported(new ZVecDiskAnnIndexParam());
        act.Should().NotThrow();
    }

    [Fact]
    public void NativeIndexParamBuilder_DiskAnn_ThrowsOnNonLinux()
    {
        if (OperatingSystem.IsLinux())
        {
            Assert.Skip("Current OS is Linux; DiskANN builder gate cannot be asserted here.");
        }

        var act = () =>
        {
            using var _ = new NativeIndexParamBuilder(new ZVecDiskAnnIndexParam());
        };
        act.Should().Throw<PlatformNotSupportedException>()
            .WithMessage(ZVecDefaults.Errors.DiskAnnRequiresLinuxLibaio);
    }
}
