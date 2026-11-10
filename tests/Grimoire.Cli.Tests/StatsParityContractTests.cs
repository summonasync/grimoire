using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Grimoire.Api;
using Grimoire.Api.Contracts;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class StatsParityContractTests : IDisposable
{
    private static readonly ParityField[] Fields =
    [
        new("Active", "Active: ", "active"),
        new("ActiveByScope", "project: ", "activeByScope.project"),
        new("Superseded", "Superseded: ", "superseded"),
        new("Tombstoned", "Tombstoned: ", "tombstoned"),
        new("PendingCompaction", "Pending compaction: ", "pendingCompaction"),
        new("IdempotencyConflicts", "Idempotency conflicts: ", "idempotencyConflicts"),
        new("Gc", "GC: ", "gc.lastSuccessAt"),
        new("Consolidation", "Consolidation: ", "consolidation.lastSuccessAt"),
        new("Tome", "Tome: ", "tome"),
        new("TomeSource", "(default)", "tomeSource"),
        new("ByTome", "Per tome: ", "byTome"),
        new("TomeStats.Active", "(2 active", "byTome.alpha.active"),
        new("TomeStats.Tombstoned", "1 tombstoned", "byTome.beta.tombstoned"),
        new("LastWriteByTome", "lastWrite ", "byTome.alpha.lastWriteAt"),
        new("TomeStats.LastWriteAt", "lastWrite ", "byTome.beta.lastWriteAt"),
        new("ByProject", "Per project: ", "byProject"),
        new("PendingCompactionByTome", "Per-tome compaction: ", "pendingCompactionByTome"),
    ];

    private static readonly string[] DeliberateDeltas =
    [
        // The CLI renders the source as prose in the Tome line ("claim file"), the API serializes
        // the TomeSource enum token ("claimFile"): one field, rendering only.
        "TomeSource: CLI prose rendering vs API enum token.",
    ];

    private static readonly DateTimeOffset Start = FakeClock.DefaultStart;

    private readonly TestHost _host = new();
    private ServiceProvider? _apiServices;
    private WebApplication? _api;
    private HttpClient? _client;

    public StatsParityContractTests()
    {
        Seed("alpha journal one", "alpha", MemoryScope.Project, Start.AddDays(-3));
        Seed("alpha journal two", "alpha", MemoryScope.Project, Start.AddDays(-1));
        Seed(
            "alpha buried",
            "alpha",
            MemoryScope.Project,
            Start.AddDays(-12),
            MemoryStatus.Tombstoned,
            Start.AddDays(-2));
        Seed("beta session note", "beta", MemoryScope.Session, Start.AddDays(-4));
        Seed("beta conflict one", "beta", MemoryScope.Global, Start.AddHours(-2), idempotencyKey: "parity-key");
        Seed("beta conflict two", "beta", MemoryScope.Global, Start.AddHours(-2), idempotencyKey: "parity-key");
        Seed(
            "beta buried",
            "beta",
            MemoryScope.Global,
            Start.AddDays(-20),
            MemoryStatus.Tombstoned,
            Start.AddDays(-5));
        _host.GcState.Current = new GcState
        {
            LastAttemptAt = Start.AddMinutes(-10),
            LastSuccessAt = Start.AddMinutes(-10),
            RemovedExpired = 2,
            RemovedTombstones = 1,
        };
        _host.ConsolidationState.Current = new ConsolidationState
        {
            LastAttemptAt = Start.AddMinutes(-10),
            LastSuccessAt = Start.AddMinutes(-10),
            ConsolidatedClusters = 3,
        };
    }

    [Fact]
    public async Task BothFaces_ListEveryChecklistField_OnASeededStore()
    {
        var output = await CliStatsAsync();
        var body = await ApiStatsAsync();

        foreach (var field in Fields)
        {
            output.ShouldContain(field.CliMarker);
            At(body, field.ApiPath).ShouldNotBeNull($"{field.Field} is missing from the API face at {field.ApiPath}");
        }
    }

    [Fact]
    public void Checklist_CoversEveryStatsProperty_Exactly()
    {
        var covered = Fields.Select(field => field.Field).ToHashSet();
        var declared =
            typeof(MemoryStats).GetProperties().Select(property => property.Name)
                .Concat(typeof(StatsResponse).GetProperties().Select(property => property.Name))
                .Concat(typeof(TomeStats).GetProperties().Select(property => "TomeStats." + property.Name))
                .Concat(typeof(TomeStatsResponse).GetProperties().Select(property => "TomeStats." + property.Name))
                .ToHashSet();

        covered.Except(declared).ShouldBe([], "the checklist names a field no stats surface declares");
        declared.Except(covered).ShouldBe([], "a stats property is missing from the parity checklist");
    }

    [Fact]
    public void Deltas_NameCoveredFields()
    {
        var covered = Fields.Select(field => field.Field).ToHashSet();

        foreach (var delta in DeliberateDeltas)
        {
            covered.ShouldContain(delta[..delta.IndexOf(':')]);
        }
    }

    [Fact]
    public async Task CliFace_ReportsTheSeededTotals()
    {
        var output = await CliStatsAsync();

        output.ShouldContain("Active: 5 (session: 1, project: 2, global: 2, personal: 0)");
        output.ShouldContain("Superseded: 0");
        output.ShouldContain("Tombstoned: 2");
        output.ShouldContain("Pending compaction: 0");
        output.ShouldContain("Idempotency conflicts: 1");
        output.ShouldContain(
            "GC: last attempt " +
            $"{FormatMoment(Start.AddMinutes(-10))}, last success {FormatMoment(Start.AddMinutes(-10))}, " +
            "removed 2 expired / 1 tombstones, last error: none, stale: false");
        output.ShouldContain("Tome: default (default)");
        output.ShouldContain(
            "Per tome: alpha (2 active, 1 tombstoned, lastWrite " +
            $"{FormatMoment(Start.AddDays(-1))}), beta (3 active, 1 tombstoned, lastWrite {FormatMoment(Start.AddHours(-2))})");
        output.ShouldContain("Per project: (none)");
        output.ShouldContain("Per-tome compaction: alpha (0), beta (0)");
    }

    [Fact]
    public async Task ApiFace_ReportsTheSeededTotals()
    {
        var body = await ApiStatsAsync();

        body.GetProperty("tome").GetString().ShouldBe("default");
        body.GetProperty("tomeSource").GetString().ShouldBe("default");
        body.GetProperty("active").GetInt64().ShouldBe(5);
        body.GetProperty("superseded").GetInt64().ShouldBe(0);
        body.GetProperty("tombstoned").GetInt64().ShouldBe(2);
        body.GetProperty("pendingCompaction").GetInt64().ShouldBe(0);
        body.GetProperty("idempotencyConflicts").GetInt64().ShouldBe(1);
        body.GetProperty("activeByScope").GetProperty("project").GetInt64().ShouldBe(2);
        body.GetProperty("activeByScope").GetProperty("session").GetInt64().ShouldBe(1);
        body.GetProperty("activeByScope").GetProperty("global").GetInt64().ShouldBe(2);
        body.GetProperty("gc").GetProperty("lastSuccessAt").GetDateTimeOffset().ShouldBe(Start.AddMinutes(-10));
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("active").GetInt64().ShouldBe(2);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("tombstoned").GetInt64().ShouldBe(1);
        body.GetProperty("byTome").GetProperty("beta").GetProperty("active").GetInt64().ShouldBe(3);
        body.GetProperty("byTome").GetProperty("alpha").GetProperty("lastWriteAt").GetDateTimeOffset()
            .ShouldBe(Start.AddDays(-1));
        body.GetProperty("byTome").GetProperty("beta").GetProperty("lastWriteAt").GetDateTimeOffset()
            .ShouldBe(Start.AddHours(-2));
        body.GetProperty("byProject").EnumerateObject().ShouldBeEmpty();
        body.GetProperty("pendingCompactionByTome").GetProperty("alpha").GetInt64().ShouldBe(0);
        body.GetProperty("pendingCompactionByTome").GetProperty("beta").GetInt64().ShouldBe(0);
    }

    private async Task<string> CliStatsAsync()
    {
        var code = await _host.RunAsync("stats", "--include-tome-compaction");

        code.ShouldBe(ExitCodes.Ok, _host.Output.ToString());
        return _host.Output.ToString();
    }

    private async Task<JsonElement> ApiStatsAsync()
    {
        if (_client is null)
        {
            _apiServices = BuildApiServices();
            _api = ApiHostBuilder.Build(new ApiHostOptions
            {
                Port = 0,
                GcIntervalMinutes = 60,
                StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", "parity-missing-export"),
            }, _apiServices);
            await _api.StartAsync();
            var address = _api.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();
            _client = new HttpClient { BaseAddress = new Uri(address) };
        }

        var response = await _client.GetAsync("/api/stats?includeTomeCompaction=true");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private ServiceProvider BuildApiServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryStore>(_host.Store);
        services.AddSingleton<ITomeManifestStore>(new FakeTomeManifestStore());
        services.AddSingleton<IEmbeddingProvider>(_host.Embeddings);
        services.AddSingleton<IClock>(_host.Clock);
        services.AddSingleton<IGcStateStore>(_host.GcState);
        services.AddSingleton<ICallerPathProvider>(new FixedCallerPath("/agents/parity-tests"));
        services.AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(60) });
        services.AddSingleton(new MemoryEngineOptions());
        return services.BuildServiceProvider();
    }

    private void Seed(
        string content,
        string tome,
        MemoryScope scope,
        DateTimeOffset createdAt,
        MemoryStatus status = MemoryStatus.Active,
        DateTimeOffset? tombstonedAt = null,
        string? idempotencyKey = null)
    {
        _host.Store.Seed(new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = scope,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Human,
            Tome = tome,
            CreatedAt = createdAt,
            LastAccessedAt = createdAt,
            Status = status,
            TombstoneReason = status == MemoryStatus.Tombstoned ? "parity burial" : null,
            TombstonedAt = tombstonedAt,
            IdempotencyKey = idempotencyKey,
        }, [1f, 0f]);
    }

    private static string FormatMoment(DateTimeOffset moment) => moment.ToString("u");

    private static JsonElement? At(JsonElement node, string path)
    {
        foreach (var segment in path.Split('.'))
        {
            if (node.ValueKind is not JsonValueKind.Object || !node.TryGetProperty(segment, out node))
            {
                return null;
            }
        }

        return node;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _api?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _apiServices?.Dispose();
        _host.Dispose();
    }

    private sealed record ParityField(string Field, string CliMarker, string ApiPath);
}
