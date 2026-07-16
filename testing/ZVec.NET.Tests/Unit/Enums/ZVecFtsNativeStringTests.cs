using ZVec.NET.Internal;
using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Enums;

public class ZVecFtsNativeStringTests
{
    [Fact]
    public void ZVecFtsTokenizer_ToNative_ExactLiterals()
    {
        ZVecNativeStrings.ToNative(ZVecFtsTokenizer.Standard).Should().Be("standard");
        ZVecNativeStrings.ToNative(ZVecFtsTokenizer.Jieba).Should().Be("jieba");
        ZVecNativeStrings.ToNative(ZVecFtsTokenizer.Whitespace).Should().Be("whitespace");
    }

    [Fact]
    public void ZVecFtsTokenFilter_ToNative_ExactLiterals()
    {
        ZVecNativeStrings.ToNative(ZVecFtsTokenFilter.Lowercase).Should().Be("lowercase");
        ZVecNativeStrings.ToNative(ZVecFtsTokenFilter.AsciiFolding).Should().Be("ascii_folding");
        ZVecNativeStrings.ToNative(ZVecFtsTokenFilter.Stemmer).Should().Be("stemmer");
    }

    [Fact]
    public void ZVecJiebaCutMode_ToNative_ExactLiterals()
    {
        ZVecNativeStrings.ToNative(ZVecJiebaCutMode.Search).Should().Be("search");
        ZVecNativeStrings.ToNative(ZVecJiebaCutMode.Mix).Should().Be("mix");
        ZVecNativeStrings.ToNative(ZVecJiebaCutMode.Full).Should().Be("full");
        ZVecNativeStrings.ToNative(ZVecJiebaCutMode.Hmm).Should().Be("hmm");
    }

    [Fact]
    public void ZVecFtsDefaultOperator_ToNative_ExactLiterals()
    {
        ZVecNativeStrings.ToNative(ZVecFtsDefaultOperator.Or).Should().Be("OR");
        ZVecNativeStrings.ToNative(ZVecFtsDefaultOperator.And).Should().Be("AND");
    }

    [Fact]
    public void ZVecFtsTokenizer_Unknown_Throws()
    {
        var act = () => ZVecNativeStrings.ToNative((ZVecFtsTokenizer)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
