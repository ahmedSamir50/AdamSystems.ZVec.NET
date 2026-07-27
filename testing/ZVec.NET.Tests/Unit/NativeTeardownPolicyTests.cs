using FluentAssertions;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Internal;

namespace ZVec.NET.Tests.Unit;

public class NativeTeardownPolicyTests
{
    [Fact]
    public void ShouldSuppressNativeTeardown_Auto_IsFalse()
    {
        // Default policy is Auto until first Initialize overrides it.
        ZVecNativeLifecycle.ShouldSuppressNativeTeardown.Should().BeFalse();
    }

    [Fact]
    public void CollectionOpenMode_Default_IsOpenOrCreate()
    {
        var opts = new ZVecCollectionRegistrationOptions();
        opts.OpenMode.Should().Be(ZVecCollectionOpenMode.OpenOrCreate);
    }

#pragma warning disable CS0618 // Obsolete Create shim
    [Fact]
    public void Create_Shim_MapsToCreateOnly_And_OpenOnly()
    {
        var opts = new ZVecCollectionRegistrationOptions { Create = true };
        opts.OpenMode.Should().Be(ZVecCollectionOpenMode.CreateOnly);

        opts.Create = false;
        opts.OpenMode.Should().Be(ZVecCollectionOpenMode.OpenOnly);
    }
#pragma warning restore CS0618
}
