using System.Text.Json;
using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class BriefHookEnvelopeTests
{
    [Fact]
    public async Task BriefHook_Purity_OneJsonObjectOnStdoutWarningsOnStderr()
    {
        using var host = new HookHost(new MemoryEngineOptions { DailyTimeZone = "Not/AZone" });
        SeedJournal(host, "the seam held");
        SeedDailyNote(host, "daily note inside the envelope");

        var code = await host.RunAsync("brief", "--format", "hook", "--daily");

        code.ShouldBe(ExitCodes.Ok, host.Error.ToString());
        var stdout = host.Output.ToString();
        stdout.TrimEnd('\n').ShouldNotContain("\n", customMessage: "the envelope must be a single line of stdout.");
        using var document = JsonDocument.Parse(stdout);
        var output = document.RootElement.GetProperty("hookSpecificOutput");
        output.GetProperty("hookEventName").GetString().ShouldBe("SessionStart");
        var context = output.GetProperty("additionalContext").GetString();
        context.ShouldNotBeNullOrWhiteSpace();
        context!.ShouldContain("the seam held");
        context.ShouldContain("daily note inside the envelope");
        context.ShouldNotContain("did not resolve");
        host.Error.ToString().ShouldContain(
            "Warning: DailyTimeZone 'Not/AZone' did not resolve; daily buckets use UTC.");
        stdout.ShouldNotContain("Warning");
    }

    [Fact]
    public async Task BriefHook_TagFree_RewardsAngleBracketShapesToSquareBrackets()
    {
        using var host = new HookHost();
        SeedFact(host, "<goal>ship it on friday</goal>");
        SeedJournal(host, "config wants <alias>+<vendor> plus <removed 2026-11-05 — superseded>");
        SeedTombstone(host, "<removed 2026-10-01 — wrong scope>");
        host.Manifests.Seed(new TomeManifest
        {
            Key = "crew",
            Description = "<startup contract> read it",
            CreatedBy = "human",
            CreatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
        });

        var code = await host.RunAsync("brief", "--format", "hook");

        code.ShouldBe(ExitCodes.Ok, host.Error.ToString());
        using var document = JsonDocument.Parse(host.Output.ToString());
        var context = document.RootElement
            .GetProperty("hookSpecificOutput")
            .GetProperty("additionalContext")
            .GetString()!;
        context.ShouldNotContain("<");
        context.ShouldNotContain(">");
        context.ShouldContain("[goal]ship it on friday[/goal]");
        context.ShouldContain("[alias]+[vendor]");
        context.ShouldContain("[removed 2026-11-05 — superseded]");
        context.ShouldContain("[removed 2026-10-01 — wrong scope]");
        context.ShouldContain("[startup contract] read it");
    }

    [Fact]
    public async Task BriefHook_StoreDown_PrintsNoEnvelopeOnStdoutAndReportsOnStderr()
    {
        using var host = new HookHost();
        host.Store.ListFailure = new UpstreamUnavailableException("qdrant");

        var code = await host.RunAsync("brief", "--format", "hook");

        code.ShouldBe(ExitCodes.UpstreamUnavailable);
        host.Output.ToString().ShouldBeEmpty();
        host.Error.ToString().ShouldContain("error [upstream_unavailable]");
    }

    private static void SeedJournal(HookHost host, string content) =>
        host.SeedRecord(content, MemoryScope.Project, MemoryKind.Journal);

    private static void SeedFact(HookHost host, string content) =>
        host.SeedRecord(content, MemoryScope.Project, MemoryKind.Fact);

    private static void SeedTombstone(HookHost host, string reason) =>
        host.SeedRecord("buried note", MemoryScope.Project, MemoryKind.Journal,
            status: MemoryStatus.Tombstoned, tombstoneReason: reason);

    private static void SeedDailyNote(HookHost host, string content)
    {
        var created = FakeClock.DefaultStart;
        host.Seed(new MemoryRecord
        {
            Id = Guid.NewGuid(),
            Content = content,
            Scope = MemoryScope.Project,
            Kind = MemoryKind.Journal,
            Actor = MemoryActor.Agent("medivh", "claude"),
            Tags = ["daily-notes"],
            CreatedAt = created,
            LastAccessedAt = created,
        });
    }

    internal sealed class HookHost : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "grimoire-hook-tests", Guid.NewGuid().ToString("N"));

        public HookHost(MemoryEngineOptions? options = null)
        {
            Store = new RecordingMemoryStore();
            Manifests = new FakeTomeManifestStore();
            ConfigPath = Path.Combine(_directory, "config.json");
            Services = TestServices.Build(
                Store,
                new StubEmbeddings(new Dictionary<string, float[]>()),
                new FakeClock(),
                options,
                configPath: ConfigPath,
                board: new FakeBoardProjectSource(),
                manifests: Manifests);
            Output = new StringWriter();
            Error = new StringWriter();
            Env = new CliEnvironment
            {
                ConfigPath = ConfigPath,
                Config = new ConfigLoader(ConfigPath, new Dictionary<string, string>()),
                Output = Output,
                Error = Error,
                Services = Services,
                Variables = new Dictionary<string, string>(),
                WorkingDirectory = _directory,
            };
        }

        public RecordingMemoryStore Store { get; }

        public FakeTomeManifestStore Manifests { get; }

        public StringWriter Output { get; }

        public StringWriter Error { get; }

        public ServiceProvider Services { get; }

        public string ConfigPath { get; }

        public CliEnvironment Env { get; }

        public void Seed(MemoryRecord record) => Store.Seed(record, [1f, 0f]);

        public void SeedRecord(
            string content,
            MemoryScope scope,
            MemoryKind kind,
            MemoryStatus status = MemoryStatus.Active,
            string? tombstoneReason = null)
        {
            var created = FakeClock.DefaultStart;
            Store.Seed(
                new MemoryRecord
                {
                    Id = Guid.NewGuid(),
                    Content = content,
                    Scope = scope,
                    Kind = kind,
                    Actor = MemoryActor.Human,
                    Tags = [],
                    CreatedAt = created,
                    LastAccessedAt = created,
                    Status = status,
                    TombstoneReason = tombstoneReason,
                    TombstonedAt = status == MemoryStatus.Tombstoned ? created : null,
                },
                [1f, 0f]);
        }

        public Task<int> RunAsync(params string[] args) => CliApplication.RunAsync(args, Env);

        public void Dispose()
        {
            Services.Dispose();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }
    }
}
