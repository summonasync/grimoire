using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class NumericGrammarTests
{
    [Theory]
    [InlineData("1e3", 1000d)]
    [InlineData("1E+3", 1000d)]
    [InlineData("2.5", 2.5d)]
    [InlineData("0.5", 0.5d)]
    [InlineData("-1", -1d)]
    [InlineData("0", 0d)]
    [InlineData("+7", 7d)]
    [InlineData("2147483647", 2147483647d)]
    [InlineData(" 4 ", 4d)]
    public void Classify_AcceptsInvariantLiteralsAsFinite(string value, double expected)
    {
        var verdict = NumericGrammar.Classify(value, out var number);

        verdict.ShouldBe(NumberVerdict.Finite);
        number.ShouldBe(expected);
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("1,000")]
    [InlineData("abc")]
    [InlineData("12a")]
    [InlineData("1 2")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Classify_RejectsTextThatIsNotANumber(string? value)
    {
        var verdict = NumericGrammar.Classify(value, out var number);

        verdict.ShouldBe(NumberVerdict.NotNumber);
        number.ShouldBe(0d);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("-1e999")]
    public void Classify_RefusesLiteralsThatParseButAreNotFinite(string value)
    {
        var verdict = NumericGrammar.Classify(value, out _);

        verdict.ShouldBe(NumberVerdict.NonFinite);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("-1", -1)]
    [InlineData("500", 500)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("-2147483648", -2147483648)]
    [InlineData("1e3", 1000)]
    [InlineData("1E+3", 1000)]
    [InlineData("2.0", 2)]
    [InlineData(" 4 ", 4)]
    public void TryWholeNumber_AcceptsExactWholeForms(string value, int expected)
    {
        var accepted = NumericGrammar.TryWholeNumber(value, out var whole);

        accepted.ShouldBeTrue();
        whole.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("0.5")]
    [InlineData("1e-1")]
    [InlineData("1e0.5")]
    [InlineData("1,000")]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void TryWholeNumber_RejectsFractionsAndExponentToFraction(string? value)
    {
        var accepted = NumericGrammar.TryWholeNumber(value, out var whole);

        accepted.ShouldBeFalse();
        whole.ShouldBe(0);
    }

    [Theory]
    [InlineData("1e999")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("9007199254740993")]
    public void TryWholeNumber_RejectsOverflowInsteadOfRoundingToANeighbour(string? value)
    {
        var accepted = NumericGrammar.TryWholeNumber(value, out var whole);

        accepted.ShouldBeFalse();
        whole.ShouldBe(0);
    }

    [Fact]
    public void TryWholeNumber_YieldsTheSameIntegersClassifyCallsFinite()
    {
        foreach (var candidate in new[] { "7", "1e3", "-2", "2147483647" })
        {
            NumericGrammar.TryWholeNumber(candidate, out var whole).ShouldBeTrue();
            NumericGrammar.Classify(candidate, out var number).ShouldBe(NumberVerdict.Finite);
            number.ShouldBe((double)whole);
        }
    }
}
