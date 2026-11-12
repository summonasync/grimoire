using System.Text;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

// serve pipes the Cli Serilog sink and this host's MEL console into one stdout, so the Api copy of
// the stamp verdict is muted on the console provider only. The three cases below are the fence: the
// console stays silent for SpaHosting, the record survives for every other provider, and the rule
// does not spill over onto the sweeper categories.
public sealed class SpaHostingConsoleFilterTests
{
    private const string SpaHostingProbe = "stamp-verdict-probe-text";
    private const string SweeperProbe = "gc-sweep-probe-text";

    [Fact]
    public async Task SpaHostingWarning_DoesNotRenderOnTheConsole()
    {
        var rendered = await CaptureConsoleAsync(host =>
        {
            var logger = host.LoggerFactory.CreateLogger("SpaHosting");
            logger.LogWarning("deployed SPA release stamp {Probe}", SpaHostingProbe);
            host.RenderProbe(SweeperProbe);
        });

        rendered.ShouldNotContain(SpaHostingProbe);
    }

    [Fact]
    public void SpaHostingWarning_StillReachesOtherProviders()
    {
        using var host = Startup();
        var records = host.Records;

        host.LoggerFactory.CreateLogger("SpaHosting")
            .LogWarning("deployed SPA release stamp {Probe}", SpaHostingProbe);

        var record = records.ShouldHaveSingleItem();
        record.Category.ShouldBe("SpaHosting");
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain(SpaHostingProbe);
    }

    [Fact]
    public async Task OtherApiCategory_StillRendersOnTheConsole()
    {
        var rendered = await CaptureConsoleAsync(host => host.RenderProbe(SweeperProbe));

        rendered.ShouldContain(SweeperProbe);
        rendered.ShouldContain("Grimoire.Api.GcSweeper");
    }

    private static async Task<string> CaptureConsoleAsync(Action<TestHost> act)
    {
        var mirror = new ConsoleMirror();
        var original = Console.Out;
        Console.SetOut(mirror);
        try
        {
            using var host = Startup();
            act(host);
            // The console logger drains its queue on its own thread in arrival order, so the probe
            // that rides the same provider proves the earlier ones already landed — and proves the
            // mirror is wired at all, which is what keeps the SpaHosting absence from being a tautology.
            await Wait.UntilAsync(() => mirror.Text.Contains(SweeperProbe), TimeSpan.FromSeconds(15));
            return mirror.Text;
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static TestHost Startup()
    {
        var app = ApiHostBuilder.Build(new ApiHostOptions
        {
            Port = 0,
            GcIntervalMinutes = 60,
            StaticRootPath = Path.Combine(Path.GetTempPath(), "grimoire-api-tests", "console-filter"),
        }, Ports());
        var loggerFactory = (LoggerFactory)app.Services.GetRequiredService<ILoggerFactory>();
        var records = new List<SpaHostingConsoleFilterTests.LogRecord>();
        loggerFactory.AddProvider(new RecordingProvider(records));
        return new TestHost(app, loggerFactory, records);
    }

    private static ServiceProvider Ports() =>
        new ServiceCollection()
            .AddSingleton<IMemoryStore>(new FakeMemoryStore())
            .AddSingleton<ITomeManifestStore>(new FakeTomeManifestStore())
            .AddSingleton<IEmbeddingProvider>(new FakeEmbeddingProvider())
            .AddSingleton<IClock>(new FakeClock())
            .AddSingleton<IProjectRegistry>(new ProjectRegistry([]))
            .AddSingleton<ICallerPathProvider>(new FixedCallerPath(GrimoireApiFactory.DefaultCallerPath))
            .AddSingleton(new GcOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IGcStateStore>(new FakeGcStateStore())
            .AddSingleton(new ConsolidationOptions { SweepInterval = TimeSpan.FromMinutes(60) })
            .AddSingleton<IConsolidationStateStore>(new FakeConsolidationStateStore())
            .BuildServiceProvider();

    private sealed class TestHost(
        WebApplication app,
        ILoggerFactory loggerFactory,
        List<LogRecord> records) : IDisposable
    {
        public ILoggerFactory LoggerFactory => loggerFactory;

        public IReadOnlyList<LogRecord> Records => records;

        // A filtered category never enters the console queue, so the queue can only be read as empty
        // once something unfiltered is known to have passed through it.
        public void RenderProbe(string probe) =>
            LoggerFactory.CreateLogger("Grimoire.Api.GcSweeper").LogWarning("gc sweep failed {Probe}", probe);

        public void Dispose() => app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed record LogRecord(string Category, LogLevel Level, string Message);

    private sealed class RecordingProvider(List<LogRecord> records) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, records);

        public void Dispose()
        {
        }

        private sealed class Recorder(string category, List<LogRecord> records) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                records.Add(new LogRecord(category, logLevel, formatter(state, exception)));
        }
    }

    private sealed class ConsoleMirror : TextWriter
    {
        private readonly StringBuilder _buffer = new();

        public override Encoding Encoding => Encoding.UTF8;

        public string Text
        {
            get
            {
                lock (_buffer)
                {
                    return _buffer.ToString();
                }
            }
        }

        public override void Write(char value)
        {
            lock (_buffer)
            {
                _buffer.Append(value);
            }
        }

        public override void Write(string? value)
        {
            lock (_buffer)
            {
                _buffer.Append(value);
            }
        }

        public override void Write(ReadOnlySpan<char> value)
        {
            lock (_buffer)
            {
                _buffer.Append(value);
            }
        }

        public override void WriteLine(string? value) => Write((value ?? string.Empty) + Environment.NewLine);
    }
}
