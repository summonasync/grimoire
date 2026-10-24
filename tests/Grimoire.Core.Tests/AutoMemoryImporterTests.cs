using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class AutoMemoryImporterTests : IDisposable
{
    private static readonly DateTimeOffset FileStamp = FakeClock.DefaultStart.AddMinutes(-10);

    private static readonly IReadOnlyDictionary<string, float[]> BaseVectors = new Dictionary<string, float[]>
    {
        ["coffee brewing notes"] = [0f, 1f, 0f],
        ["deploy after four on fridays"] = [0.97f, 0.24f, 0f],
        ["rewritten body"] = [0f, 0f, 1f],
        ["shared fact text"] = [1f, 0f, 0f],
    };

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "grimoire-auto-memory-tests", Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, float[]> _vectors = new(BaseVectors);

    private readonly FakeMemoryStore _store = new();

    private readonly FakeEmbeddingProvider _embeddings;

    private readonly FakeClock _clock = new();

    private readonly AutoMemoryImporter _importer;

    public AutoMemoryImporterTests()
    {
        Directory.CreateDirectory(_directory);
        _embeddings = new FakeEmbeddingProvider(_vectors);
        _importer = new AutoMemoryImporter(
            TestMemories.Service(_store, _embeddings, _clock),
            _store,
            _embeddings,
            _clock);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    [Fact]
    public async Task Parse_FactFiles_ProducesOrdinalOrderedCandidates()
    {
        WriteFile("b.md", "second fact");
        WriteFile("a.md", "first fact");

        var candidates = (await Parse()).Candidates;

        candidates.Select(candidate => candidate.FileName).ShouldBe(["a.md", "b.md"]);
        candidates[0].Content.ShouldBe("first fact");
    }

    [Fact]
    public async Task Parse_FrontMatterBlock_IsStrippedFromContent()
    {
        WriteFile("a.md", "---\nname: deploy\ndescription: notes\n---\ndeploy body");

        var candidates = (await Parse()).Candidates;

        candidates.ShouldHaveSingleItem().Content.ShouldBe("deploy body");
    }

    [Fact]
    public async Task Parse_FrontMatterWithoutClosingMarker_IsToleratedAsBody()
    {
        var text = "---\nname: unterminated\nbody stays whole";
        WriteFile("a.md", text);

        var candidates = (await Parse()).Candidates;

        candidates.ShouldHaveSingleItem().Content.ShouldBe(text);
    }

    [Fact]
    public async Task Parse_MemoryIndexWithLinksOnly_IsNotACandidate()
    {
        WriteFile("MEMORY.md", "# Memory Index\n\n- [deploy](deploy.md)\n1. [ops](ops.md)\n");
        WriteFile("deploy.md", "deploy fact");

        var candidates = (await Parse()).Candidates;

        candidates.Select(candidate => candidate.FileName).ShouldBe(["deploy.md"]);
    }

    [Fact]
    public async Task Parse_MemoryIndexWithProseLine_IsACandidate()
    {
        WriteFile("MEMORY.md", "# Memory Index\n\n- [deploy](deploy.md)\nOwner prefers Friday deploys.\n");

        var candidates = (await Parse()).Candidates;

        var index = candidates.ShouldHaveSingleItem();
        index.FileName.ShouldBe("MEMORY.md");
        index.Content.ShouldContain("Owner prefers Friday deploys.");
    }

    [Fact]
    public async Task Parse_EmptyBodies_AreSkipped()
    {
        WriteFile("empty.md", string.Empty);
        WriteFile("blank.md", "\n  \n\t\n");

        var parse = await Parse();

        parse.Candidates.ShouldBeEmpty();
        parse.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Parse_UnreadableFile_IsReportedAsFailureInsteadOfThrowing()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("locked.md", "locked body");
        StripReadPermissions(Path.Combine(_directory, "locked.md"));

        var parse = await Parse();

        parse.Candidates.Select(candidate => candidate.FileName).ShouldBe(["a.md"]);
        var failure = parse.Failures.ShouldHaveSingleItem();
        failure.FileName.ShouldBe("locked.md");
        failure.Reason.ShouldNotBeNull();
    }

    [Fact]
    public async Task Import_UnreadableFile_FailsPerFileAndImportsTheRest()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("locked.md", "locked body");
        StripReadPermissions(Path.Combine(_directory, "locked.md"));

        var summary = await Import();

        summary.Files.ShouldBe(2);
        summary.Imported.ShouldBe(1);
        summary.Failed.ShouldBe(1);
        var outcome = summary.Outcomes.Single(item => item.Kind == AutoMemoryOutcomeKind.Failed);
        outcome.FileName.ShouldBe("locked.md");
        outcome.Reason.ShouldNotBeNull();
        _store.Records.Select(record => record.Tags.Single()).ShouldBe(["auto-memory:a.md"]);
        var marker = (await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None)).State.ShouldNotBeNull();
        marker.Hashes.Keys.ShouldBe(["a.md"]);
    }

    [Fact]
    public async Task Read_MissingMarker_ReturnsAbsentWithoutWarning()
    {
        var read = await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None);

        read.State.ShouldBeNull();
        read.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task Write_ThenRead_RoundtripsState()
    {
        var state = new AutoMemoryImportState
        {
            ImportedAt = FakeClock.DefaultStart,
            SourcePath = _directory,
            Hashes = new Dictionary<string, string> { ["a.md"] = "aa11", ["b.md"] = "bb22" },
        };

        await AutoMemoryImportState.WriteAsync(_directory, state, CancellationToken.None);
        var read = await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None);

        read.Warning.ShouldBeNull();
        var restored = read.State.ShouldNotBeNull();
        restored.ImportedAt.ShouldBe(state.ImportedAt);
        restored.SourcePath.ShouldBe(_directory);
        restored.Hashes.Count.ShouldBe(2);
        restored.Hashes["a.md"].ShouldBe("aa11");
        restored.Hashes["b.md"].ShouldBe("bb22");
    }

    [Fact]
    public async Task Read_CorruptJson_ReturnsAbsentWithWarning()
    {
        File.WriteAllText(AutoMemoryImportState.PathFor(_directory), "{ this is not json");

        var read = await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None);

        read.State.ShouldBeNull();
        read.Warning.ShouldNotBeNull().ShouldContain("not valid JSON");
    }

    [Fact]
    public async Task Read_MarkerMissingFields_ReturnsAbsentWithWarning()
    {
        File.WriteAllText(AutoMemoryImportState.PathFor(_directory), "{}");

        var read = await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None);

        read.State.ShouldBeNull();
        read.Warning.ShouldNotBeNull().ShouldContain("missing required fields");
    }

    [Fact]
    public async Task Write_TargetPathOccupiedByDirectory_FailsWithoutLeavingTempFiles()
    {
        Directory.CreateDirectory(AutoMemoryImportState.PathFor(_directory));

        await Should.ThrowAsync<IOException>(() => AutoMemoryImportState.WriteAsync(
            _directory,
            new AutoMemoryImportState
            {
                ImportedAt = FakeClock.DefaultStart,
                SourcePath = _directory,
                Hashes = new Dictionary<string, string>(),
            },
            CancellationToken.None));

        Directory.EnumerateFiles(_directory, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void ContentHash_IsDeterministicHexOfBody()
    {
        var hash = AutoMemoryImportState.ContentHash("same body");

        hash.ShouldBe(AutoMemoryImportState.ContentHash("same body"));
        hash.ShouldNotBe(AutoMemoryImportState.ContentHash("other body"));
        hash.Length.ShouldBe(64);
        hash.All(char.IsAsciiHexDigit).ShouldBeTrue();
    }

    [Fact]
    public async Task Import_FirstRun_RemembersAllFilesWithProvenance()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");

        var summary = await Import();

        summary.Files.ShouldBe(2);
        summary.Imported.ShouldBe(2);
        summary.SkippedDuplicates.ShouldBe(0);
        summary.Failed.ShouldBe(0);
        summary.Unchanged.ShouldBe(0);
        summary.Warnings.ShouldBeEmpty();

        var records = _store.Records;
        records.Count.ShouldBe(2);
        var byTag = records.ToDictionary(record => record.Tags.Single(), record => record);
        byTag.ShouldContainKey("auto-memory:a.md");
        byTag.ShouldContainKey("auto-memory:b.md");
        byTag["auto-memory:a.md"].Scope.ShouldBe(MemoryScope.Project);
        byTag["auto-memory:a.md"].Kind.ShouldBe(MemoryKind.Fact);
        byTag["auto-memory:a.md"].Tome.ShouldBe("ops");
        byTag["auto-memory:a.md"].ProjectId.ShouldBe("grim");
        byTag["auto-memory:a.md"].Actor.ShouldBe(TestMemories.Agent);
    }

    [Fact]
    public async Task Import_SecondRun_IsIdempotentWithoutReprobing()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");
        await Import();

        var embedsBefore = _embeddings.Requests.Count;
        var upsertsBefore = _store.UpsertCount;

        var summary = await Import();

        summary.Imported.ShouldBe(0);
        summary.Failed.ShouldBe(0);
        summary.Unchanged.ShouldBe(2);
        summary.Outcomes.ShouldBeEmpty();
        _embeddings.Requests.Count.ShouldBe(embedsBefore);
        _store.UpsertCount.ShouldBe(upsertsBefore);
    }

    [Fact]
    public async Task Import_NearDuplicateOfExistingFact_SkipsWithWinnerId()
    {
        var existing = TestMemories.Create(
            "deploy after four on friday",
            [1f, 0f, 0f],
            kind: MemoryKind.Fact,
            projectId: "grim",
            tome: "ops");
        _store.Seed(existing, [1f, 0f, 0f]);
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");

        var summary = await Import();

        summary.Imported.ShouldBe(1);
        summary.SkippedDuplicates.ShouldBe(1);
        var skip = summary.Outcomes.Single(outcome => outcome.Kind == AutoMemoryOutcomeKind.SkippedDuplicate);
        skip.FileName.ShouldBe("b.md");
        skip.WinnerId.ShouldBe(existing.Id);
        skip.RecordId.ShouldBeNull();
        _store.Records.ShouldNotContain(record => record.Content == "deploy after four on fridays");
    }

    [Fact]
    public async Task Import_CorruptMarker_WarnsAndImportsAsAbsent()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "shared fact text");
        File.WriteAllText(AutoMemoryImportState.PathFor(_directory), "{ this is not json");

        var summary = await Import();

        summary.Warnings.Count.ShouldBe(1);
        summary.Warnings[0].ShouldContain("not valid JSON");
        summary.Imported.ShouldBe(2);
    }

    [Fact]
    public async Task Import_UnadvancedMarker_RerunSkipsAsDuplicates()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "shared fact text");
        var first = await Import();
        File.WriteAllText(AutoMemoryImportState.PathFor(_directory), "{ this is not json");

        var second = await Import();

        second.Imported.ShouldBe(0);
        second.Failed.ShouldBe(0);
        second.SkippedDuplicates.ShouldBe(2);
        second.Files.ShouldBe(2);
        var byFile = second.Outcomes.ToDictionary(outcome => outcome.FileName);
        byFile["a.md"].WinnerId.ShouldBe(first.Outcomes.Single(outcome => outcome.FileName == "a.md").RecordId);
        byFile["b.md"].WinnerId.ShouldBe(first.Outcomes.Single(outcome => outcome.FileName == "b.md").RecordId);
        _store.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Import_ChangedFile_IsReimportedWhileOthersUnchanged()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");
        await Import();
        WriteFile("a.md", "rewritten body");

        var summary = await Import();

        summary.Imported.ShouldBe(1);
        summary.Unchanged.ShouldBe(1);
        summary.Outcomes.Single(outcome => outcome.Kind == AutoMemoryOutcomeKind.Imported).FileName.ShouldBe("a.md");
        _store.Records.ShouldContain(record => record.Content == "rewritten body");
        _store.Records.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Import_TouchedFileWithSameHash_IsReprobedWithoutDuplicating()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "deploy after four on fridays");
        var first = await Import();
        File.SetLastWriteTimeUtc(Path.Combine(_directory, "a.md"), _clock.UtcNow.AddMinutes(5).UtcDateTime);

        var second = await Import();

        second.Imported.ShouldBe(0);
        second.SkippedDuplicates.ShouldBe(1);
        second.Unchanged.ShouldBe(1);
        var reprobed = second.Outcomes.Single();
        reprobed.FileName.ShouldBe("a.md");
        reprobed.Kind.ShouldBe(AutoMemoryOutcomeKind.SkippedDuplicate);
        reprobed.WinnerId.ShouldBe(first.Outcomes.Single(outcome => outcome.FileName == "a.md").RecordId);
        _store.Records.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Import_FailedFile_KeepsMarkerStateAndRetriesNextRun()
    {
        WriteFile("a.md", "coffee brewing notes");
        WriteFile("b.md", "retry me");

        var first = await Import();

        first.Imported.ShouldBe(1);
        first.Failed.ShouldBe(1);
        var failure = first.Outcomes.Single(outcome => outcome.Kind == AutoMemoryOutcomeKind.Failed);
        failure.FileName.ShouldBe("b.md");
        failure.Reason.ShouldNotBeNull();
        var marker = (await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None)).State.ShouldNotBeNull();
        marker.Hashes.ShouldContainKey("a.md");
        marker.Hashes.ShouldNotContainKey("b.md");

        _vectors["retry me"] = [1f, 1f, 0f];
        var second = await Import();

        second.Failed.ShouldBe(0);
        second.Imported.ShouldBe(1);
        second.Unchanged.ShouldBe(1);
        second.Outcomes.Single(outcome => outcome.FileName == "b.md").Kind.ShouldBe(AutoMemoryOutcomeKind.Imported);
        var advanced = (await AutoMemoryImportState.ReadAsync(_directory, CancellationToken.None)).State.ShouldNotBeNull();
        advanced.Hashes.ShouldContainKey("b.md");
    }

    [Fact]
    public async Task Import_IdenticalContentWithinBatch_SecondFileSkipsWithFirstAsWinner()
    {
        WriteFile("a.md", "shared fact text");
        WriteFile("b.md", "shared fact text");

        var summary = await Import();

        summary.Imported.ShouldBe(1);
        summary.SkippedDuplicates.ShouldBe(1);
        var winner = summary.Outcomes.Single(outcome => outcome.Kind == AutoMemoryOutcomeKind.Imported);
        winner.FileName.ShouldBe("a.md");
        summary.Outcomes.Single(outcome => outcome.Kind == AutoMemoryOutcomeKind.SkippedDuplicate)
            .WinnerId.ShouldBe(winner.RecordId);
        _store.Records.Count.ShouldBe(1);
    }

    private Task<AutoMemoryParseResult> Parse() =>
        AutoMemoryFileParser.ParseAsync(_directory, CancellationToken.None);

    private Task<AutoMemoryImportSummary> Import(string? tome = "ops", string? projectId = "grim") =>
        _importer.ImportAsync(_directory, tome, TestMemories.Agent, projectId, CancellationToken.None);

    private void WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
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
}
