using FluentAssertions;

namespace ZVec.NET.Tests.Unit.IndexParams;

public class ZVecFtsExtraParamsTests
{
    [Fact]
    public void ToNativeJson_Empty_ReturnsNull()
    {
        var extra = new ZVecFtsExtraParams();
        extra.ToNativeJson().Should().BeNull();
    }

    [Fact]
    public void ToNativeJson_MaxTokenLength()
    {
        var extra = new ZVecFtsExtraParams { MaxTokenLength = 128 };
        extra.ToNativeJson().Should().Be("{\"max_token_length\":128}");
    }

    [Fact]
    public void ToNativeJson_CutMode()
    {
        var extra = new ZVecFtsExtraParams { CutMode = ZVecJiebaCutMode.Search };
        extra.ToNativeJson().Should().Be("{\"cut_mode\":\"search\"}");
    }

    [Fact]
    public void ToNativeJson_CombinedKeys()
    {
        var extra = new ZVecFtsExtraParams
        {
            JiebaDictDir = @"C:\dicts",
            CutMode = ZVecJiebaCutMode.Mix,
            StemmerLang = "porter"
        };
        extra.ToNativeJson().Should().Be(
            "{\"jieba_dict_dir\":\"C:\\\\dicts\",\"cut_mode\":\"mix\",\"stemmer_lang\":\"porter\"}");
    }

    [Fact]
    public void ToNativeJson_EscapesQuotes()
    {
        var extra = new ZVecFtsExtraParams { UserDictPath = "path\"with\"quotes" };
        extra.ToNativeJson().Should().Be("{\"user_dict_path\":\"path\\\"with\\\"quotes\"}");
    }
}
