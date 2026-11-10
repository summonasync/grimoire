using System.Text.Json;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class AutoMemoryImportCommandTests : IDisposable
{
    private static readonly DateTimeOffset FileStamp = FakeClock.DefaultStart.AddMinutes(-10);

    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["coffee brewing notes"] = [0f, 1f, 0f],
        ["deploy after four on fridays"] = [1f, 0f, 0f],
        ["locked body"] = [0f, 0f, 1f],
    };

    private readonly string _source = Path.Combine(
        Path.GetTempPath(), "grimoire-auto-memory-cli", Guid.NewGuid().ToString("N"));

    private readonly TestHost _host;

    public AutoMemoryImportCommandTests()
    {
        Directory.CreateDirectory(_source);
        _host = NewHost();
    }

    [Fact]
    public async Task Help_ListsTheAutoMemoryVerb()
    {
        (await _host.RunAsync("help")).ShouldBe(ExitCodes.Ok);
        _host.Output.ToString().ShouldContain("auto-memory");
        _host.Output.GetStringBuilder().Clear();

        var code = await _host.RunAsync("help", "auto-memory");

        code.ShouldBe(ExitCodes.Ok);
        var output = _host.Output.ToString();
        output.ShouldContain("Usage: grimoire auto-memory import --source <dir>");
        output.ShouldContain("--source");
        output.ShouldContain("--tome");
        output.ShouldContain("--json");
        output.ShouldContain("auto-memory:<file>");
    }

    [Fact]
    public async Task FirstRun_ImportsFilesWithProvenanceTagsAndProjectTag()
    {
        using var host = NewHost([new ProjectAliasRecord { Alias = "grim", Paths = [_source] }]);
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");

        var code = await host.RunAsync("auto-memory", "import", "--source", _source);

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var output = host.Output.ToString();
        output.ShouldContain("Files: 2");
        output.ShouldContain("Imported: 2");
        output.ShouldContain("Skipped duplicates: 0");
        output.ShouldContain("Failed: 0");
        output.ShouldContain("Project: grim");

        var records = host.Store.Records;
        records.Count.ShouldBe(2);
        records.SelectMany(record => record.Tags).Order()
            .ShouldBe(["auto-memory:a.md", "auto-memory:b.md"]);
        var first = records.Single(record => record.Tags.Contains("auto-memory:a.md"));
        first.Scope.ShouldBe(MemoryScope.Project);
        first.Kind.ShouldBe(MemoryKind.Fact);
        first.ProjectId.ShouldBe("grim");
        first.Tome.ShouldBe("default");
        first.Actor.ShouldBe(MemoryActor.Human);
    }

    [Fact]
    public async Task SecondRun_ImportsNothingAndExitsZero()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");
        await _host.RunAsync("auto-memory", "import", "--source", _source);
        _host.Output.GetStringBuilder().Clear();

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Imported: 0");
        output.ShouldContain("Skipped duplicates: 0");
        output.ShouldContain("Failed: 0");
        output.ShouldContain("Unchanged: 2");
        _host.Store.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Json_SummaryIsParseableAndComplete()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source, "--json");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        using var document = JsonDocument.Parse(_host.Output.ToString());
        var root = document.RootElement;
        root.GetProperty("tome").GetString().ShouldNotBeNull();
        root.GetProperty("summary").GetProperty("files").GetInt32().ShouldBe(2);
        root.GetProperty("summary").GetProperty("imported").GetInt32().ShouldBe(2);
        root.GetProperty("summary").GetProperty("skippedDuplicates").GetInt32().ShouldBe(0);
        root.GetProperty("summary").GetProperty("failed").GetInt32().ShouldBe(0);
        root.GetProperty("summary").GetProperty("unchanged").GetInt32().ShouldBe(0);
        root.GetProperty("summary").GetProperty("warnings").ValueKind.ShouldBe(JsonValueKind.Array);

        var outcomes = root.GetProperty("summary").GetProperty("outcomes");
        outcomes.ValueKind.ShouldBe(JsonValueKind.Array);
        var byFile = outcomes.EnumerateArray()
            .ToDictionary(outcome => outcome.GetProperty("fileName").GetString()!);
        byFile.Keys.ShouldBe(["a.md", "b.md"]);
        byFile["a.md"].GetProperty("kind").GetString().ShouldBe("imported");
        byFile["a.md"].GetProperty("recordId").GetString().ShouldNotBeNull();
        byFile["a.md"].TryGetProperty("winnerId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Json_RegisteredProject_AppearsInPayload()
    {
        WriteFile("a.md", "coffee brewing notes");
        using var host = NewHost([new ProjectAliasRecord { Alias = "grim", Paths = [_source] }]);

        var code = await host.RunAsync("auto-memory", "import", "--source", _source, "--json");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        using var document = JsonDocument.Parse(host.Output.ToString());
        var root = document.RootElement;
        root.GetProperty("project").GetString().ShouldBe("grim");
        root.TryGetProperty("projectWarning", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Json_UnregisteredSource_CarriesRegistryWarning()
    {
        WriteFile("a.md", "coffee brewing notes");

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source, "--json");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        using var document = JsonDocument.Parse(_host.Output.ToString());
        var root = document.RootElement;
        root.TryGetProperty("project", out _).ShouldBeFalse();
        root.GetProperty("projectWarning").GetString().ShouldContain("no registered project matches");
    }

    [Fact]
    public async Task UnregisteredSource_WarnsAndImportsWithoutProjectTag()
    {
        WriteFile("a.md", "coffee brewing notes");

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Project: (none)");
        output.ShouldContain($"Warning: no registered project matches the source directory '{_source}'");
        var record = _host.Store.Records.ShouldHaveSingleItem();
        record.ProjectId.ShouldBeNull();
        record.Tags.ShouldContain("auto-memory:a.md");
    }

    [Fact]
    public async Task ExplicitTome_LandsOnImportedRecords()
    {
        WriteFile("a.md", "coffee brewing notes");

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source, "--tome", "ops");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("Tome: ops");
        _host.Store.Records.ShouldHaveSingleItem().Tome.ShouldBe("ops");
    }

    [Fact]
    public async Task UnreadableFile_FailsPerFileInsteadOfCrashing()
    {
        WriteFile("a.md", "coffee brewing notes");
        var lockedPath = Path.Combine(_source, "locked.md");
        File.WriteAllText(lockedPath, "locked body");
        File.SetLastWriteTimeUtc(lockedPath, FileStamp.UtcDateTime);
        StripReadPermissions(lockedPath);

        var code = await _host.RunAsync("auto-memory", "import", "--source", _source);

        code.ShouldBe(ExitCodes.Runtime, _host.Output.ToString());
        var output = _host.Output.ToString();
        output.ShouldContain("Imported: 1");
        output.ShouldContain("Failed: 1");
        output.ShouldContain("locked.md: failed - ");
        _host.Store.Records.ShouldHaveSingleItem().Tags.ShouldContain("auto-memory:a.md");

        var marker = (await AutoMemoryImportState.ReadAsync(_source, CancellationToken.None)).State.ShouldNotBeNull();
        marker.Hashes.Keys.ShouldBe(["a.md"]);
    }

    [Fact]
    public async Task ImportAction_ContractViolations_AreUsageErrors()
    {
        (await _host.RunAsync("auto-memory")).ShouldBe(ExitCodes.Usage);
        (await _host.RunAsync("auto-memory", "frobnicate")).ShouldBe(ExitCodes.Usage);
        (await _host.RunAsync("auto-memory", "import")).ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("Option 'source' requires");
        _host.Output.GetStringBuilder().Clear();

        var code = await _host.RunAsync("auto-memory", "import", "--source", Path.Combine(_source, "missing"));

        code.ShouldBe(ExitCodes.Usage);
        _host.Output.ToString().ShouldContain("does not exist");
    }

    private TestHost NewHost(IReadOnlyList<ProjectAliasRecord>? projects = null) => new(
        embeddings: new StubEmbeddings(new Dictionary<string, float[]>(Vectors)),
        variables: new Dictionary<string, string>(),
        projects: projects);

    private void WriteFile(string name, string content)
    {
        var path = Path.Combine(_source, name);
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, FileStamp.UtcDateTime);
    }

    private static void StripReadPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The unreadable-file scenario needs Unix file modes.");
        }

        File.SetUnixFileMode(path, UnixFileMode.None);
    }

    public void Dispose()
    {
        _host.Dispose();
        try
        {
            Directory.Delete(_source, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }
}
