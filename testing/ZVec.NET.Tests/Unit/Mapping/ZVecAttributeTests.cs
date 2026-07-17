using FluentAssertions;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.Mapping;

public class ZVecAttributeTests
{
    [Fact]
    public void ZVecFieldAttribute_StoresNameOverride()
    {
        var attr = new ZVecFieldAttribute("category") { Nullable = true };
        attr.Name.Should().Be("category");
        attr.Nullable.Should().BeTrue();
    }

    [Fact]
    public void ZVecVectorAttribute_RequiresPositiveDimension()
    {
        var act = () => new ZVecVectorAttribute(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ZVecCollectionAttribute_RequiresName()
    {
        var act = () => new ZVecCollectionAttribute(" ");
        act.Should().Throw<ArgumentException>();
    }
}
