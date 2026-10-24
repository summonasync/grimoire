using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class LexicalTermMatcherTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("--- !? ---")]
    public void Tokenize_WithoutLetterOrDigitRuns_YieldsNoTerms(string query)
    {
        LexicalTermMatcher.Tokenize(query).ShouldBe([]);
    }

    [Fact]
    public void Tokenize_SplitsOnNonAlphanumericCharacters_AndLowercases()
    {
        LexicalTermMatcher.Tokenize("Memory_Scope v2, RECALL! déjà-vu").ShouldBe(["memory", "scope", "v2", "recall", "déjà", "vu"]);
    }

    [Fact]
    public void Tokenize_DuplicateTerms_AppearOnceInFirstSeenOrder()
    {
        LexicalTermMatcher.Tokenize("tome Tome TOME! tomb").ShouldBe(["tome", "tomb"]);
    }

    [Fact]
    public void Match_WithEmptyTerms_YieldsNoSpans()
    {
        var record = TestMemories.Create("alpha squad decision", []);

        LexicalTermMatcher.Match([], record).ShouldBe([]);
    }

    [Fact]
    public void Match_WithoutAnyContainedTerm_YieldsNoSpans()
    {
        var record = TestMemories.Create("alpha squad decision", []);

        LexicalTermMatcher.Match(["gamma"], record).ShouldBe([]);
    }

    [Fact]
    public void Match_TermInsideContent_ProducesAContentSpanWithItsOffset()
    {
        var record = TestMemories.Create("keep the rig grounded", []);

        LexicalTermMatcher.Match(["rig"], record).ShouldBe([new TermSpan("rig", "content", 9)]);
    }

    [Fact]
    public void Match_TagOnlyTerm_FallsBackToTheFirstContainingTag()
    {
        var record = TestMemories.Create("alpha squad decision", [], tags: ["squad-lead", "ops"]);

        LexicalTermMatcher.Match(["ops"], record).ShouldBe([new TermSpan("ops", "tag", 0)]);
    }

    [Fact]
    public void Match_IsCaseInsensitiveOnBothSides()
    {
        var record = TestMemories.Create("Alpha Squad Decision", [], tags: ["OPS"]);
        var terms = LexicalTermMatcher.Tokenize("alpha squad ops");

        LexicalTermMatcher.Match(terms, record).ShouldBe(
        [
            new TermSpan("alpha", "content", 0),
            new TermSpan("squad", "content", 6),
            new TermSpan("ops", "tag", 0),
        ]);
    }

    [Fact]
    public void Match_TermPresentInContentAndTags_ContentWins()
    {
        var record = TestMemories.Create("squad goals", [], tags: ["squad"]);

        LexicalTermMatcher.Match(["squad"], record).ShouldBe([new TermSpan("squad", "content", 0)]);
    }

    [Fact]
    public void Match_MixedFields_KeepQueryOrderAcrossContentAndTags()
    {
        var record = TestMemories.Create("vector recall plan", [], tags: ["tome-a", "hybrid"]);

        LexicalTermMatcher.Match(["vector", "hybrid", "plan"], record).ShouldBe(
        [
            new TermSpan("vector", "content", 0),
            new TermSpan("hybrid", "tag", 0),
            new TermSpan("plan", "content", 14),
        ]);
    }

    [Fact]
    public void Match_UsesSubstringContainment_InsideLongerWords()
    {
        var record = TestMemories.Create("reindexing the tome", []);

        LexicalTermMatcher.Match(["index"], record).ShouldBe([new TermSpan("index", "content", 2)]);
    }

    [Fact]
    public void Match_DuplicateInputTerms_ProduceOneSpanEach_WhileTokenizedTermsStayDistinct()
    {
        var record = TestMemories.Create("tome tome", []);

        LexicalTermMatcher.Match(["tome", "tome"], record).Count.ShouldBe(2);
        LexicalTermMatcher.Match(LexicalTermMatcher.Tokenize("tome TOME"), record)
            .ShouldBe([new TermSpan("tome", "content", 0)]);
    }
}
