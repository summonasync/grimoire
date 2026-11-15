using System.Globalization;
using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

public sealed class ReadmeToolTableTests
{
    private const int ContractedToolCount = 16;

    [Fact]
    public void ToolTable_ListsEveryContractedToolExactlyOnce()
    {
        var names = ReadmeSurface.ToolTableNames(ReadmeSurface.ReadmeText());

        ContractedToolNames.All.Length.ShouldBe(ContractedToolCount, customMessage:
            "the contracted roster is no longer 16 tools — re-check the README table denominator with it");
        names.Length.ShouldBe(ContractedToolCount, customMessage:
            $"README lists {names.Length} tool rows, the contract is {ContractedToolCount} — " +
            $"missing [{string.Join(", ", ContractedToolNames.All.Except(names, StringComparer.Ordinal))}], " +
            $"not contracted [{string.Join(", ", names.Except(ContractedToolNames.All, StringComparer.Ordinal))}]");
        names.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(ContractedToolNames.All);
    }

    public static TheoryData<string, string> TopicVotingKnobs()
    {
        var options = new MemoryEngineOptions();

        return new TheoryData<string, string>
        {
            { nameof(options.NearTopicSimilarityThreshold), Format(options.NearTopicSimilarityThreshold) },
            { nameof(options.TopicAgreementCount), Format(options.TopicAgreementCount) },
            { nameof(options.TopicConfidenceThreshold), Format(options.TopicConfidenceThreshold) },
        };
    }

    [Theory]
    [MemberData(nameof(TopicVotingKnobs))]
    public void TopicVotingFamily_IsDocumentedWithTheCodeDefault(string knob, string codeDefault)
    {
        var readme = ReadmeSurface.ReadmeText();

        readme.ShouldContain($"`{knob}` ({codeDefault})", customMessage:
            $"'{knob}' is either undocumented in README.md or documented at a value the code does not carry ({codeDefault})");
    }

    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
