using System.Text.Json;
using Grimoire.Cli;
using Grimoire.Cli.Commands;
using Grimoire.Cli.Export;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ExportCommandTests : IDisposable
{
    private const string TargetModel = "nomic-embed-text";

    private readonly TestHost _host = new();

    [Fact]
    public async Task RunAsync_Export_WritesHeaderWithConfiguredFingerprint()
    {
        var path = TempPath("dump.jsonl");

        var code = await _host.RunAsync("export", path);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        var header = HeaderOf(path);
        header.FormatVersion.ShouldBe(ExportFormat.Version);
        header.Fingerprint.ShouldBe(EmbeddingFingerprint.From(TargetModel, 768));
        header.ExportedAt.ShouldBe(FakeClock.DefaultStart);
    }

    [Fact]
    public async Task RunAsync_Export_IncludesTombstonedAndSupersededWithVectors()
    {
        var active = Seed(Record("active fact"), 1, 0);
        var superseded = Seed(Record("superseded fact", status: MemoryStatus.Superseded, supersededBy: active.Id), 0.5f, 0.5f);
        var tombstoned = Seed(Record("gone fact", status: MemoryStatus.Tombstoned, tombstoneReason: "wrong"), 0, 1);

        var path = TempPath("dump.jsonl");
        (await _host.RunAsync("export", path)).ShouldBe(ExitCodes.Ok);

        var lines = File.ReadAllLines(path);
        lines.Length.ShouldBe(4);

        var entries = lines[1..].Select(EntryOf).ToArray();
        entries.Select(entry => entry.Record.Id).ShouldBe([active.Id, superseded.Id, tombstoned.Id]);
        entries.Select(entry => entry.Vector).ShouldBe(
        [
            new float[] { 1, 0 },
            new float[] { 0.5f, 0.5f },
            new float[] { 0, 1 },
        ]);
        entries.Single(entry => entry.Record.Id == tombstoned.Id).Record.TombstoneReason.ShouldBe("wrong");
        entries.Single(entry => entry.Record.Id == superseded.Id).Record.SupersededBy.ShouldBe(active.Id);
    }

    [Fact]
    public async Task RunAsync_Export_Gzip_WritesGzipStream()
    {
        Seed(Record("kept"), 1, 0);
        var path = TempPath("dump.jsonl.gz");

        var code = await _host.RunAsync("export", path, "--gzip");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        File.ReadAllBytes(path)[..2].ShouldBe([0x1f, 0x8b]);
    }

    [Fact]
    public void ResolvePath_WithoutExplicitPath_FallsBackToHomeExportName()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        ExportCommand.ResolvePath(_host.Env, null, gzip: false, FakeClock.DefaultStart)
            .ShouldBe(Path.Combine(home, ".grimoire", "export-20261017T120000.jsonl"));
        ExportCommand.ResolvePath(_host.Env, null, gzip: true, FakeClock.DefaultStart)
            .ShouldBe(Path.Combine(home, ".grimoire", "export-20261017T120000.jsonl.gz"));
    }

    [Fact]
    public void ResolvePath_WithRelativePath_ResolvesAgainstTheWorkingDirectory()
    {
        ExportCommand.ResolvePath(_host.Env, "dump.jsonl", gzip: false, FakeClock.DefaultStart)
            .ShouldBe(Path.Combine(_host.Env.WorkingDirectory, "dump.jsonl"));
    }

    private MemoryRecord Seed(MemoryRecord record, params float[] vector)
    {
        _host.Store.Seed(record, vector);
        return record;
    }

    private static MemoryRecord Record(
        string content,
        MemoryStatus status = MemoryStatus.Active,
        Guid? supersededBy = null,
        string? tombstoneReason = null,
        string? idempotencyKey = null,
        string? tome = null) => new()
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Fact,
            Actor = MemoryActor.Agent("dotnet-coder", "claude"),
            Tome = tome ?? TomeKey.DefaultTome,
            Tags = ["ops"],
            Importance = 0.75,
            CreatedAt = FakeClock.DefaultStart,
            LastAccessedAt = FakeClock.DefaultStart,
            AccessCount = 3,
            Sources = [Guid.NewGuid()],
            Corrections =
        [
            new CorrectionEntry
            {
                At = FakeClock.DefaultStart,
                Actor = MemoryActor.Human,
                Action = CorrectionAction.Patch,
                Reason = "typo",
            },
        ],
            IdempotencyKey = idempotencyKey,
            Status = status,
            SupersededBy = supersededBy,
            TombstoneReason = tombstoneReason,
            TombstonedAt = tombstoneReason is null ? null : FakeClock.DefaultStart,
        };

    private static ExportHeader HeaderOf(string path)
    {
        var header = JsonSerializer.Deserialize<ExportHeader>(File.ReadLines(path).First(), ExportFormat.Json);
        header.ShouldNotBeNull();
        return header;
    }

    private static ExportEntry EntryOf(string line)
    {
        var entry = JsonSerializer.Deserialize<ExportEntry>(line, ExportFormat.Json);
        entry.ShouldNotBeNull();
        return entry;
    }

    private string TempPath(string name) => Path.Combine(_host.Env.WorkingDirectory, name);

    public void Dispose() => _host.Dispose();
}
