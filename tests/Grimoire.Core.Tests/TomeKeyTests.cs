using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class TomeKeyTests
{
    [Theory]
    [InlineData("default")]
    [InlineData("feature-008")]
    [InlineData("007-tome")]
    [InlineData("a")]
    public void Normalize_KeepsCanonicalKeysUntouched(string key)
    {
        TomeKey.Normalize(key).ShouldBe(key);
    }

    [Theory]
    [InlineData("My Tome", "my-tome")]
    [InlineData("product_assistants", "product-assistants")]
    [InlineData("  Mixed_Case Name  ", "mixed-case-name")]
    [InlineData("Johan-Boolman", "johan-boolman")]
    public void Normalize_TrimsLowercasesAndKebabs(string candidate, string expected)
    {
        TomeKey.Normalize(candidate).ShouldBe(expected);
    }

    [Theory]
    [InlineData("too--many   dashes", "too-many-dashes")]
    [InlineData("__under__scores__", "under-scores")]
    public void Normalize_CollapsesRepeatingSeparators(string candidate, string expected)
    {
        TomeKey.Normalize(candidate).ShouldBe(expected);
    }

    [Theory]
    [InlineData("-leading-and-trailing-", "leading-and-trailing")]
    [InlineData(" - spaced - ", "spaced")]
    public void Normalize_StripsEdgeDashes(string candidate, string expected)
    {
        TomeKey.Normalize(candidate).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_RejectsMissingKeys(string? candidate)
    {
        Should.Throw<ArgumentException>(() => TomeKey.Normalize(candidate));
    }

    [Theory]
    [InlineData("tome;drop")]
    [InlineData("home/tome")]
    [InlineData("том")]
    [InlineData("tome!")]
    public void Normalize_RejectsInvalidCharset(string candidate)
    {
        Should.Throw<ArgumentException>(() => TomeKey.Normalize(candidate));
    }

    [Fact]
    public void Normalize_RejectsKeysOverSixtyFourCharacters()
    {
        var candidate = new string('a', TomeKey.MaxLength + 1);

        Should.Throw<ArgumentException>(() => TomeKey.Normalize(candidate));
    }

    [Fact]
    public void Normalize_AcceptsKeysAtMaxLength()
    {
        var candidate = new string('a', TomeKey.MaxLength);

        TomeKey.Normalize(candidate).ShouldBe(candidate);
    }

    [Fact]
    public void Normalize_RejectsCandidatesThatCollapseToNothing()
    {
        Should.Throw<ArgumentException>(() => TomeKey.Normalize("---"));
    }

    [Fact]
    public void TryNormalize_AcceptsValidCandidatesAndYieldsTheNormalizedKey()
    {
        var accepted = TomeKey.TryNormalize("My Tome", out var normalized);

        accepted.ShouldBeTrue();
        normalized.ShouldBe("my-tome");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tome!")]
    public void TryNormalize_RejectsInvalidCandidates(string? candidate)
    {
        var accepted = TomeKey.TryNormalize(candidate, out var normalized);

        accepted.ShouldBeFalse();
        normalized.ShouldBeNull();
    }
}
