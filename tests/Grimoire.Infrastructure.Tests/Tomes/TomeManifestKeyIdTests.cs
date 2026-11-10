using Grimoire.Infrastructure.Tomes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Tomes;

public sealed class TomeManifestKeyIdTests
{
    [Theory]
    [InlineData("dev", "cd5f9a9a-ca98-5750-baac-5a5645bbf25f")]
    [InlineData("env", "f3c3a177-f89f-53ae-b043-a8e986735ec6")]
    [InlineData("default", "7df55a80-bc17-5024-8b91-4dde17b6430d")]
    public void For_WithSameKey_ProducesTheStableReferenceUuid(string key, string expected)
    {
        TomeManifestKeyId.For(key).ShouldBe(Guid.Parse(expected));
    }

    [Fact]
    public void For_WithSameKey_IsDeterministicAcrossCalls()
    {
        TomeManifestKeyId.For("dev").ShouldBe(TomeManifestKeyId.For("dev"));
    }

    [Fact]
    public void For_WithDistinctKeys_ProducesDistinctIds()
    {
        TomeManifestKeyId.For("dev").ShouldNotBe(TomeManifestKeyId.For("env"));
        TomeManifestKeyId.For("dev").ShouldNotBe(TomeManifestKeyId.For("social"));
    }

    [Theory]
    [InlineData("dev")]
    [InlineData("meta")]
    public void For_CarriesUuidVersionFiveAndRfcVariantBits(string key)
    {
        var text = TomeManifestKeyId.For(key).ToString("N");

        text[12].ShouldBe('5');
        text[16].ShouldBeOneOf('8', '9', 'a', 'b');
    }
}
