using System.Text.Json;
using Grimoire.Cli.Export;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class ImportCommandTests : IDisposable
{
    private const int ImportBatchSize = 256;

    private readonly TestHost _host = new();

    [Fact]
    public async Task RunAsync_ExportWipeImport_RestoresIdenticalState()
    {
        var active = Seed(Record(
            "active fact",
            idempotencyKey: "remember-1",
            tome: "dotnet_coder"),
            1, 0);
        var superseded = Seed(Record("superseded fact", status: MemoryStatus.Superseded, supersededBy: active.Id), 0.5f, 0.5f);
        var tombstoned = Seed(Record("gone fact", status: MemoryStatus.Tombstoned, tombstoneReason: "wrong"), 0, 1);
        var path = TempPath("dump.jsonl");

        (await _host.RunAsync("export", path)).ShouldBe(ExitCodes.Ok);
        var before = Snapshot();
        _host.Store.Clear();

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        Snapshot().ShouldBe(before);
        _host.Output.ToString().ShouldContain("Import complete: 3/3 records");
        _host.Store.BatchUpserted.Select(batch => batch.Count).ShouldBe([3]);
    }

    [Fact]
    public async Task RunAsync_Reimport_KeepsStateIdentical()
    {
        Seed(Record("kept fact"), 1, 0);
        var path = TempPath("dump.jsonl");
        (await _host.RunAsync("export", path)).ShouldBe(ExitCodes.Ok);
        (await _host.RunAsync("import", path)).ShouldBe(ExitCodes.Ok);
        var afterFirst = Snapshot();

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        Snapshot().ShouldBe(afterFirst);
        _host.Store.BatchUpserted.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_GzipExport_RestoresIdenticalState()
    {
        Seed(Record("gzipped fact"), 0, 1);
        var path = TempPath("dump.jsonl.gz");
        (await _host.RunAsync("export", path, "--gzip")).ShouldBe(ExitCodes.Ok);
        var before = Snapshot();
        _host.Store.Clear();

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        Snapshot().ShouldBe(before);
    }

    [Fact]
    public async Task RunAsync_LargeExport_StreamsInBoundedUpsertBatches()
    {
        for (var index = 0; index < ImportBatchSize * 2 + 88; index++)
        {
            Seed(Record($"bulk fact {index}"), index % 2, 1);
        }

        var path = TempPath("dump.jsonl");
        (await _host.RunAsync("export", path)).ShouldBe(ExitCodes.Ok);
        var before = Snapshot();
        _host.Store.Clear();

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        Snapshot().ShouldBe(before);
        _host.Store.BatchUpserted.Select(batch => batch.Count).ShouldBe([ImportBatchSize, ImportBatchSize, 88]);
        _host.Store.Records.Count.ShouldBe(ImportBatchSize * 2 + 88);
        _host.Output.ToString().ShouldContain("256/600 imported");
        _host.Output.ToString().ShouldContain("Import complete: 600/600 records");
    }

    [Fact]
    public async Task RunAsync_FingerprintMismatch_RefusesWithReindexHint()
    {
        var path = WriteExport(
            new ExportHeader
            {
                FormatVersion = ExportFormat.Version,
                Fingerprint = EmbeddingFingerprint.From("other-model", 3),
                ExportedAt = FakeClock.DefaultStart,
            },
            new ExportEntry { Record = Record("stray fact"), Vector = [1f, 2f, 3f] });

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.ReindexRequired, _host.Output.ToString());
        _host.Output.ToString().ShouldContain("error [reindex_required]");
        _host.Output.ToString().ShouldContain("run `grimoire reindex`");
        _host.Store.Records.ShouldBeEmpty();
        _host.Store.BatchUpserted.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_UnsupportedFormatVersion_RefusesBeforeAnyWrite()
    {
        var path = WriteExport(
            new ExportHeader
            {
                FormatVersion = ExportFormat.Version + 1,
                Fingerprint = EmbeddingFingerprint.From(new EmbeddingOptions().Model, new EmbeddingOptions().Dimensions),
                ExportedAt = FakeClock.DefaultStart,
            },
            new ExportEntry { Record = Record("stray fact"), Vector = [1f, 0] });

        var code = await _host.RunAsync("import", path);

        code.ShouldBe(ExitCodes.Runtime, _host.Output.ToString());
        _host.Output.ToString().ShouldContain($"Unsupported export format version {ExportFormat.Version + 1}");
        _host.Store.Records.ShouldBeEmpty();
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

    private IReadOnlyDictionary<Guid, (string Json, string VectorJson)> Snapshot() => _host.Store.Records
        .ToDictionary(
            record => record.Id,
            record => (
                JsonSerializer.Serialize(record, ExportFormat.Json),
                JsonSerializer.Serialize(_host.Store.VectorOf(record.Id))));

    private string WriteExport(ExportHeader header, params ExportEntry[] entries) =>
        WriteExport(header, false, entries);

    private string WriteExport(ExportHeader header, bool gzip, params ExportEntry[] entries)
    {
        var path = TempPath(gzip ? "dump.jsonl.gz" : "dump.jsonl");
        Directory.CreateDirectory(_host.Env.WorkingDirectory);
        using var stream = ExportFile.Create(path, gzip);
        using var writer = new StreamWriter(stream);
        writer.WriteLine(JsonSerializer.Serialize(header, ExportFormat.Json));

        foreach (var entry in entries)
        {
            writer.WriteLine(JsonSerializer.Serialize(entry, ExportFormat.Json));
        }

        return path;
    }

    private string TempPath(string name) => Path.Combine(_host.Env.WorkingDirectory, name);

    public void Dispose() => _host.Dispose();
}
