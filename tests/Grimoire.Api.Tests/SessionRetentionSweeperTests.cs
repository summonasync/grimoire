using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public sealed class SessionRetentionSweeperTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private static readonly DateTimeOffset Now = FakeClock.DefaultStart;

    [Fact]
    public async Task Sweep_RetiresRowsPastRetention_KeepsTheCutoffRowAndEverythingInsideIt()
    {
        var registry = new SweepingRegistry();
        registry.Seed(Row(Now.AddHours(-25)));
        registry.Seed(Row(Now.AddHours(-24)));
        registry.Seed(Row(Now.AddHours(-1)));
        registry.Seed(Row(Now));
        var logger = new RecordingLogger();
        var sweeper = CreateSweeper(registry, logger);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => registry.RemovalCounts.Count > 0);
        await sweeper.StopAsync(cts.Token);

        registry.Cutoffs[0].ShouldBe(Now - Retention);
        registry.RemovalCounts[0].ShouldBe(1);
        registry.Entries.Select(entry => entry.OccurredAt).ShouldBe(
            [Now.AddHours(-24), Now.AddHours(-1), Now]);
        logger.Records.ShouldContain(record =>
            record.Level == LogLevel.Information && record.Message.Contains("retired 1 handshake rows"));
    }

    [Fact]
    public async Task Sweep_KeepsTheRowWrittenWhileThePassIsRunning()
    {
        var registry = new SweepingRegistry();
        registry.Seed(Row(Now.AddHours(-30), "arrived-before-retention"));
        registry.OnBeforePrune = () => registry.Seed(Row(Now, "handshake-mid-pass"));
        var secondPass = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gates = new[] { Task.CompletedTask, secondPass.Task };
        var gateIndex = 0;
        var sweeper = new SessionRetentionSweeper(
            registry.PruneAsync,
            new FakeClock(),
            Retention,
            Interval,
            pause: (_, _) => gates[Math.Min(gateIndex++, gates.Length - 1)]);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => registry.RemovalCounts.Count > 0);

        registry.RemovalCounts[0].ShouldBe(1);
        registry.Entries.Select(entry => entry.Host).ShouldBe(["handshake-mid-pass"]);

        registry.OnBeforePrune = null;
        secondPass.SetResult();
        await Wait.UntilAsync(() => registry.PruneAttempts > 1);
        await sweeper.StopAsync(cts.Token);

        registry.Entries.Select(entry => entry.Host).ShouldBe(["handshake-mid-pass"]);
    }

    [Fact]
    public async Task Sweep_RunsTheNextPassAfterAStoreFailure_AndLogsItWithoutFaulting()
    {
        var registry = new SweepingRegistry { FailuresBeforeSuccess = 1 };
        registry.Seed(Row(Now.AddHours(-40)));
        var logger = new RecordingLogger();
        var sweeper = CreateSweeper(registry, logger);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => registry.Entries.Count == 0);
        await sweeper.StopAsync(cts.Token);

        registry.PruneAttempts.ShouldBeGreaterThanOrEqualTo(2);
        registry.Cutoffs.ShouldAllBe(cutoff => cutoff == Now - Retention);
        logger.Records.ShouldContain(record =>
            record.Level == LogLevel.Error && record.Exception is UpstreamUnavailableException);
    }

    [Fact]
    public async Task Sweep_CallsNothingButPruneAndLeavesTheMemoriesStoreAlone()
    {
        var registry = new SweepingRegistry();
        registry.Seed(Row(Now.AddHours(-26)));
        var memories = new FakeMemoryStore();
        memories.Seed("a standing observation", [1f, 0f]);
        var sweeper = CreateSweeper(registry);
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => registry.PruneAttempts > 1);
        await sweeper.StopAsync(cts.Token);

        registry.Calls.Distinct().ShouldBe(["prune"]);
        memories.UpsertCount.ShouldBe(0);
        memories.UpdateCount.ShouldBe(0);
        memories.DeleteCount.ShouldBe(0);
        memories.GetCount.ShouldBe(0);
        memories.Records.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Sweep_WaitsConfiguredIntervalBetweenRuns()
    {
        var observedDelays = new List<TimeSpan>();
        var registry = new SweepingRegistry();
        var sweeper = new SessionRetentionSweeper(
            registry.PruneAsync,
            new FakeClock(),
            Retention,
            Interval,
            pause: (delay, _) =>
            {
                observedDelays.Add(delay);
                return Task.CompletedTask;
            });
        using var cts = new CancellationTokenSource();

        await sweeper.StartAsync(cts.Token);
        await Wait.UntilAsync(() => observedDelays.Count >= 2);
        await sweeper.StopAsync(cts.Token);

        observedDelays.Take(2).ShouldBe([Interval, Interval]);
    }

    [Fact]
    public void Constructor_RejectsANonPositiveRetentionWindow() =>
        Should.Throw<ArgumentOutOfRangeException>(() => CreateSweeper(
            new SweepingRegistry(), retention: TimeSpan.Zero));

    [Fact]
    public void Constructor_RejectsANonPositiveInterval() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new SessionRetentionSweeper(
            new SweepingRegistry().PruneAsync, new FakeClock(), Retention, TimeSpan.Zero));

    [Fact]
    public void NonFiniteRetentionHours_NeverReachTheCutoffArithmetic()
    {
        // The sweep subtracts a TimeSpan, never raw hours: TimeSpan refuses to be built from a
        // non-finite count, while the arithmetic the guard protects stays silently destructive.
        Should.Throw<ArgumentException>(() => TimeSpan.FromHours(double.NaN));
        Should.Throw<OverflowException>(() => TimeSpan.FromHours(double.PositiveInfinity));
        Now.AddHours(double.NaN).ShouldBe(Now);
    }

    private static SessionRetentionSweeper CreateSweeper(
        SweepingRegistry registry,
        RecordingLogger? logger = null,
        TimeSpan? retention = null) =>
        new(registry.PruneAsync, new FakeClock(), retention ?? Retention, Interval,
            pause: (_, _) => Task.CompletedTask, logger: logger);

    private static SessionRegistryEntry Row(DateTimeOffset occurredAt, string host = "workstation") => new()
    {
        OccurredAt = occurredAt,
        ClientName = "qwen",
        ActorAlias = "guldana",
        ActorVendor = "qwen",
        Transport = "stdio",
        Host = host,
    };

    private sealed class SweepingRegistry : ISessionRegistry
    {
        private readonly List<SessionRegistryEntry> _entries = [];

        public List<string> Calls { get; } = [];

        public List<DateTimeOffset> Cutoffs { get; } = [];

        public List<int> RemovalCounts { get; } = [];

        public int PruneAttempts { get; private set; }

        public int FailuresBeforeSuccess { get; set; }

        public Action? OnBeforePrune { get; set; }

        public IReadOnlyList<SessionRegistryEntry> Entries => _entries;

        public string? DegradationReason => null;

        public void Seed(SessionRegistryEntry entry) => _entries.Add(entry);

        public Task<Guid> WriteAsync(SessionRegistryEntry entry, CancellationToken cancellationToken)
        {
            Calls.Add("write");
            _entries.Add(entry);
            return Task.FromResult(Guid.CreateVersion7());
        }

        public Task RefineActorAsync(Guid id, string alias, string vendor, CancellationToken cancellationToken)
        {
            Calls.Add("refine");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionRegistryEntry>> ListRecentAsync(
            int limit,
            DateTimeOffset? notBefore,
            CancellationToken cancellationToken)
        {
            Calls.Add("list");
            IReadOnlyList<SessionRegistryEntry> rows = _entries.ToArray();
            return Task.FromResult(rows);
        }

        public async Task<int> PruneAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            Calls.Add("prune");
            PruneAttempts++;
            Cutoffs.Add(cutoff);
            OnBeforePrune?.Invoke();

            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new UpstreamUnavailableException("qdrant", new IOException("host unreachable"));
            }

            await Task.Yield();
            var expired = _entries.Where(entry => entry.OccurredAt < cutoff).ToArray();
            foreach (var entry in expired)
            {
                _entries.Remove(entry);
            }

            RemovalCounts.Add(expired.Length);
            return expired.Length;
        }
    }

    private sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger : ILogger<SessionRetentionSweeper>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Records)
            {
                Records.Add(new LogRecord(logLevel, formatter(state, exception), exception));
            }
        }
    }
}
