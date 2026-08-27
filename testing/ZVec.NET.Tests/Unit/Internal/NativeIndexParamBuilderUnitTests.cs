using FluentAssertions;
using System.Runtime.InteropServices;
using ZVec.NET.Internal;

namespace ZVec.NET.Tests.Unit.Internal;

/// <summary>
/// Unit-level construction contracts for index params (no native required until builder runs).
/// Native create/destroy paths live in NativeBuildersIntegrationTests.
/// </summary>
public class NativeIndexParamBuilderUnitTests
{
    [Fact]
    public void EnableRotate_Defaults_False_On_Vector_Index_Params()
    {
        new ZVecFlatIndexParam().EnableRotate.Should().BeFalse();
        new ZVecHnswIndexParam().EnableRotate.Should().BeFalse();
        new ZVecIvfIndexParam().EnableRotate.Should().BeFalse();
        new ZVecVamanaIndexParam().EnableRotate.Should().BeFalse();
        new ZVecDiskAnnIndexParam().EnableRotate.Should().BeFalse();
        ZVecDefaults.Quantizer.EnableRotate.Should().BeFalse();
    }

    [Fact]
    public void NativeIndexParamBuilder_IvfRabitqIndexParam_Type_IsSupported()
    {
        var param = new ZVecIvfRabitqIndexParam { Nlist = 8, TotalBits = 7 };
        param.MetricType.Should().Be(ZVecMetricType.L2);
        ((int)ZVecIndexType.IvfRabitq).Should().Be(7);
    }

    [Fact]
    public void NativeIndexParamBuilder_Rabitq_Throws_CApiGap_OnX64()
    {
        if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
        {
            Assert.Skip("Current process is Arm/Arm64; C API gap is asserted on x64.");
        }

        var act = () =>
        {
            using var _ = new NativeIndexParamBuilder(new ZVecHnswRabitqIndexParam());
        };
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*HNSW_RABITQ*");
    }
}
