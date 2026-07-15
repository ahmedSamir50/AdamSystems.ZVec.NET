using AdamSystems.ZVec.NET.Interop;
using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Interop;

public class SafeHandleTests
{
    [Fact]
    public void SafeZvecHandle_Metadata_ShouldBeCorrect()
    {
        var type = typeof(SafeZvecHandle);
        type.IsSealed.Should().BeTrue();
        type.BaseType.Should().Be(typeof(SafeZvecHandleBase));

        // Verify parameterless constructor exists
        var ctor = type.GetConstructor(Type.EmptyTypes);
        ctor.Should().NotBeNull();

        // Verify IntPtr constructor exists
        var intPtrCtor = type.GetConstructor([typeof(IntPtr), typeof(bool)]);
        intPtrCtor.Should().NotBeNull();
    }

    [Fact]
    public void SafeZvecSchemaHandle_Metadata_ShouldBeCorrect()
    {
        var type = typeof(SafeZvecSchemaHandle);
        type.IsSealed.Should().BeTrue();
        type.BaseType.Should().Be(typeof(SafeZvecHandleBase));

        // Verify parameterless constructor exists
        var ctor = type.GetConstructor(Type.EmptyTypes);
        ctor.Should().NotBeNull();

        // Verify IntPtr constructor exists
        var intPtrCtor = type.GetConstructor([typeof(IntPtr), typeof(bool)]);
        intPtrCtor.Should().NotBeNull();
    }

    [Fact]
    public void SafeZvecQueryHandle_Metadata_ShouldBeCorrect()
    {
        var type = typeof(SafeZvecQueryHandle);
        type.IsSealed.Should().BeTrue();
        type.BaseType.Should().Be(typeof(SafeZvecHandleBase));

        // Verify parameterless constructor exists
        var ctor = type.GetConstructor(Type.EmptyTypes);
        ctor.Should().NotBeNull();

        // Verify IntPtr constructor exists
        var intPtrCtor = type.GetConstructor([typeof(IntPtr), typeof(bool)]);
        intPtrCtor.Should().NotBeNull();
    }

    [Fact]
    public void SafeZvecHandle_IsInvalid_CorrectlyEvaluated()
    {
        var invalidHandle1 = new SafeZvecHandle(IntPtr.Zero);
        invalidHandle1.IsInvalid.Should().BeTrue();

        var invalidHandle2 = new SafeZvecHandle((IntPtr)(-1));
        invalidHandle2.IsInvalid.Should().BeTrue();

        var validHandle = new SafeZvecHandle((IntPtr)123);
        validHandle.IsInvalid.Should().BeFalse();
        
        // Suppress finalizer to avoid calling ReleaseHandle during GC
        GC.SuppressFinalize(validHandle);
    }
}
