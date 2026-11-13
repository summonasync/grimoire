using System.Globalization;
using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class CliNumberGrammarTests : IDisposable
{
    private const string ImportanceLiteral = "nightly build broke";
    private const string TtlLiteral = "session note on the pipeline";

    private readonly TestHost _host = new(new StubEmbeddings(new Dictionary<string, float[]>
    {
        [ImportanceLiteral] = [1f, 0f],
        [TtlLiteral] = [1f, 0f],
    }));

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    public async Task Remember_NonFiniteImportanceLiteral_IsRefusedAndWritesNothing(string value)
    {
        var code = await _host.RunAsync("remember", ImportanceLiteral, "--scope", "global", "--importance", value);

        code.ShouldBe(ExitCodes.Usage);
        _host.Store.Upserted.ShouldBeEmpty();
        _host.Output.ToString().ShouldContain($"--importance must be a number between 0 and 1, got '{value}'.");
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    public async Task Remember_NonFiniteTtlLiteral_IsRefusedAndWritesNothing(string value)
    {
        var code = await _host.RunAsync(
            "remember", TtlLiteral, "--scope", "session", "--session", "s1", "--ttl", value);

        code.ShouldBe(ExitCodes.Usage);
        _host.Store.Upserted.ShouldBeEmpty();
        _host.Output.ToString().ShouldContain($"--ttl must be a positive number of hours, got '{value}'.");
    }

    [Fact]
    public async Task Remember_ExponentImportanceWithinRange_IsAccepted()
    {
        var code = await _host.RunAsync("remember", ImportanceLiteral, "--scope", "global", "--importance", "1e-2");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Upserted.ShouldHaveSingleItem().Importance.ShouldBe(0.01);
    }

    [Fact]
    public async Task Remember_FractionalTtlHours_IsAccepted()
    {
        var code = await _host.RunAsync(
            "remember", TtlLiteral, "--scope", "session", "--session", "s1", "--ttl", "0.5");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.Updated.ShouldHaveSingleItem().ExpiresAt.ShouldBe(_host.Clock.UtcNow.AddHours(0.5));
    }

    [Theory]
    [InlineData("2.5")]
    [InlineData("1e0.5")]
    [InlineData("1e-1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1e999")]
    [InlineData("1e-999")]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task List_LimitLiteralOutsideTheGrammar_IsRefusedBeforeAnyRead(string value)
    {
        SeedRecords();

        var code = await _host.RunAsync("list", "--limit", value);

        code.ShouldBe(ExitCodes.Usage);
        _host.Store.ListedFilters.ShouldBeEmpty();
        _host.Output.ToString().ShouldContain($"--limit must be a positive integer, got '{value}'.");
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("+2", 2)]
    public async Task List_PlainIntegerLimit_StillAccepted(string value, int expected)
    {
        SeedRecords();

        var code = await _host.RunAsync("list", "--limit", value);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.ListedFilters.ShouldHaveSingleItem();
        OutputRecords().Length.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2.0", 2)]
    [InlineData("1.000", 1)]
    [InlineData("1e3", 4)]
    public async Task List_LimitGrammarWideningIsDeliberate_AcceptsWholeExponentAndTrailingZeroForms(
        string value, int expected)
    {
        SeedRecords();

        var code = await _host.RunAsync("list", "--limit", value);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Store.ListedFilters.ShouldHaveSingleItem();
        OutputRecords().Length.ShouldBe(expected);
    }

    [Fact]
    public void PositiveInt_LimitDoorReadsTheSameLiteralsUnderAForeignCurrentCulture()
    {
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";
        comma.NumberFormat.NumberGroupSeparator = ".";

        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = comma;
            CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator.ShouldBe(",");
            CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator.ShouldBe(".");

            CliValues.PositiveInt(ListSpec(), "limit", "2.0").ShouldBe(2);
            CliValues.PositiveInt(ListSpec(), "limit", "1e3").ShouldBe(1000);
            CliValues.PositiveInt(ListSpec(), "limit", "1.000").ShouldBe(1);
            Should.Throw<UsageException>(() => CliValues.PositiveInt(ListSpec(), "limit", "1,5"));
            Should.Throw<UsageException>(() => CliValues.PositiveInt(ListSpec(), "limit", "1,000"));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }

    [Fact]
    public void UnitAndHours_DoorsRejectTheSameNonFiniteLiteralsUnderAForeignCurrentCulture()
    {
        var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        comma.NumberFormat.NumberDecimalSeparator = ",";

        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = comma;

            CliValues.Unit(ListSpec(), "importance", "1e-2").ShouldBe(0.01);
            CliValues.Hours(ListSpec(), "ttl", "0.5").ShouldBe(0.5);
            Should.Throw<UsageException>(() => CliValues.Unit(ListSpec(), "importance", "NaN"));
            Should.Throw<UsageException>(() => CliValues.Hours(ListSpec(), "ttl", "Infinity"));
            Should.Throw<UsageException>(() => CliValues.Hours(ListSpec(), "ttl", "1e999"));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }

    public void Dispose() => _host.Dispose();

    private void SeedRecords()
    {
        _host.Store.Add("first", [1f, 0f], tags: ["a"]);
        _host.Store.Add("second", [1f, 0f], tags: ["a"]);
        _host.Store.Add("third", [1f, 0f], tags: ["a"]);
        _host.Store.Add("fourth", [1f, 0f], tags: ["a"]);
    }

    private string[] OutputRecords() => _host.Output.ToString()
        .Split('\n')
        .Where(line => line.Contains("first") || line.Contains("second")
                       || line.Contains("third") || line.Contains("fourth"))
        .Select(line => line.Trim())
        .ToArray();

    private static CommandSpec ListSpec() => new()
    {
        Name = "list",
        Summary = "List memories.",
        Syntax = "grimoire list [--limit n]",
    };
}
