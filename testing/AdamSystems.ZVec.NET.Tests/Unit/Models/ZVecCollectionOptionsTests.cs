using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Models;

public class ZVecCollectionOptionsTests
{
    [Fact]
    public void ZVecCollectionOptions_Defaults()
    {
        var options = new ZVecCollectionOptions();
        options.ReadOnly.Should().BeFalse();
        options.EnableMmap.Should().BeTrue();
        options.MaxConcurrentReads.Should().Be(ZVecDefaults.Collection.MaxConcurrentReads);
    }
}
