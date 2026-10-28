using Grimoire.Cli.Tests.Fakes;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Tests;

internal sealed class TestHost : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "grimoire-cli-tests", Guid.NewGuid().ToString("N"));

    public TestHost(
        StubEmbeddings? embeddings = null,
        MemoryEngineOptions? options = null,
        RecordingVectorDb? db = null,
        IReadOnlyDictionary<string, string>? configEnvironment = null,
        IActorClaimProvider? claims = null,
        IReadOnlyDictionary<string, string>? variables = null,
        GcOptions? gcOptions = null,
        FakeGcStateStore? gcState = null,
        ICallerPathProvider? callerPaths = null,
        IReadOnlyList<ProjectAliasRecord>? projects = null,
        FakeBoardProjectSource? board = null)
    {
        Store = new RecordingMemoryStore();
        Embeddings = embeddings ?? new StubEmbeddings(new Dictionary<string, float[]>());
        Clock = new FakeClock();
        GcState = gcState ?? new FakeGcStateStore();
        ConsolidationState = new FakeConsolidationStateStore();
        ConfigPath = Path.Combine(_directory, "config.json");
        Board = board ?? new FakeBoardProjectSource();
        Services = TestServices.Build(Store, Embeddings, Clock, options, db, claims, gcOptions, GcState, ConsolidationState, ConfigPath, callerPaths, projects, Board);
        Output = new StringWriter();
        Env = new CliEnvironment
        {
            ConfigPath = ConfigPath,
            Config = new ConfigLoader(ConfigPath, configEnvironment),
            Output = Output,
            Error = Output,
            Services = Services,
            Variables = variables,
            WorkingDirectory = _directory,
        };
    }

    public RecordingMemoryStore Store { get; }

    public StubEmbeddings Embeddings { get; }

    public FakeClock Clock { get; }

    public FakeGcStateStore GcState { get; }

    public FakeConsolidationStateStore ConsolidationState { get; }

    public ServiceProvider Services { get; }

    public string ConfigPath { get; }

    public FakeBoardProjectSource Board { get; }

    public StringWriter Output { get; }

    public CliEnvironment Env { get; }

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
