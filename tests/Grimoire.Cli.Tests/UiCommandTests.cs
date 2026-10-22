using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Grimoire.Cli.Commands;
using Grimoire.Core;
using Grimoire.Infrastructure.Autostart;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Processes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class UiCommandTests
{
    private const string Remedy = "systemctl --user start grimoire-serve";
    private const string PrettyUrl = "http://grimoire.localhost:5099";
    private const string LiteralUrl = "http://127.0.0.1:5099";
    private const string LoginBinary = "/opt/grimoire/bin/grimoire";

    private static string LauncherFile =>
        UiCommand.BrowserCommand("http://x", OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()).File;

    [Fact]
    public void DefaultTimings_HoldTheContractBudgetAndPerAttemptDeadline()
    {
        UiTimings.Default.Budget.ShouldBe(TimeSpan.FromSeconds(10));
        UiTimings.Default.AttemptDeadline.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Help_RendersUiUsageFromTheCatalog()
    {
        using var harness = new Harness();

        var code = await harness.RunAsync("ui", "--help");

        code.ShouldBe(ExitCodes.Ok);
        harness.Output.ToString().ShouldContain("Usage: grimoire ui");
        harness.Process.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task HealthAnswering_OpensOnceAndNeverTouchesSystemctl()
    {
        using var harness = new Harness(resolver: new FakeResolver(IPAddress.Loopback));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        var launch = harness.Process.Calls.ShouldHaveSingleItem();
        launch.File.ShouldBe(LauncherFile);
        launch.Args.ShouldBe([PrettyUrl]);
        harness.Output.ToString().ShouldContain(PrettyUrl);
    }

    [Fact]
    public async Task HealthDownThenUnitUp_StartsTheUnitOnceThenPollsToOpen()
    {
        using var harness = new Harness(
            probe: new ScriptedProbe(aliveFromCall: 3),
            resolver: new FakeResolver(IPAddress.Loopback),
            timings: new UiTimings(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20)));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        harness.Process.Calls.Select(call => call.File).ToList().ShouldBe(["systemctl", LauncherFile]);
        harness.Process.Calls[0].Args.ShouldBe(["--user", "start", "grimoire-serve"]);
        harness.Probe.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task HealthNeverReturns_FailsWithoutABrowserCallAndNamesTheRemedy()
    {
        using var harness = new Harness(
            probe: new ScriptedProbe(),
            resolver: new FakeResolver(IPAddress.Loopback),
            timings: new UiTimings(TimeSpan.FromMilliseconds(400), TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(20)));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.UpstreamUnavailable);
        var output = harness.Output.ToString();
        output.ShouldContain("error [console_unreachable]");
        output.ShouldContain(Remedy);
        harness.Process.Calls.ShouldHaveSingleItem().File.ShouldBe("systemctl");
        harness.Probe.Calls.ShouldBeGreaterThan(1);
        harness.Resolver.Lookups.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("127.0.0.1", PrettyUrl)]
    [InlineData("::1", PrettyUrl)]
    [InlineData("203.0.113.9", LiteralUrl)]
    public async Task DisplayUrl_FollowsWhatTheResolverAnswers(string answer, string expected)
    {
        using var harness = new Harness(resolver: new FakeResolver([IPAddress.Parse(answer)]));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        harness.Resolver.Lookups.ShouldContain(UiCommand.PrettyHost);
        harness.Process.Calls.ShouldHaveSingleItem().Args.ShouldContain(expected);
    }

    [Fact]
    public async Task DisplayUrl_FallsBackToTheLiteral_WhenTheNameResolvesToNothing()
    {
        using var harness = new Harness(resolver: new FakeResolver());

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        harness.Process.Calls.ShouldHaveSingleItem().Args.ShouldContain(LiteralUrl);
    }

    [Fact]
    public async Task PrettyDisplayUrl_StillProbesTheLoopbackLiteral()
    {
        using var console = new StubConsole(status: HttpStatusCode.OK);
        using var harness = new Harness(
            probe: new LoopbackConsoleProbe(console.Client()),
            resolver: new FakeResolver(IPAddress.Loopback),
            port: console.Port);

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        console.RequestPaths.ShouldHaveSingleItem().ShouldBe("/health");
        console.LastHostHeader.ShouldStartWith("127.0.0.1");
        harness.Process.Calls.ShouldHaveSingleItem().Args.ShouldContain($"http://grimoire.localhost:{console.Port}");
    }

    [Fact]
    public async Task HungListener_IsCutShortByItsOwnDeadline_AndBudgetExpiryStillFails()
    {
        using var hung = new HungListener();
        var probe = new CountingProbe(new LoopbackConsoleProbe(new HttpClient()));
        using var harness = new Harness(
            probe: probe,
            resolver: new FakeResolver(IPAddress.Loopback),
            port: hung.Port,
            timings: new UiTimings(TimeSpan.FromMilliseconds(900), TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(25)));

        var clock = Stopwatch.StartNew();
        var code = await harness.RunAsync("ui");
        var elapsed = clock.Elapsed;

        code.ShouldBe(ExitCodes.UpstreamUnavailable, $"{probe.Attempts} attempts in {elapsed.TotalMilliseconds:F0}ms");
        probe.Attempts.ShouldBeGreaterThanOrEqualTo(2);
        elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        harness.Process.Calls.ShouldHaveSingleItem().File.ShouldBe("systemctl");
        harness.Output.ToString().ShouldContain(Remedy);
    }

    [Fact]
    public async Task WedgedSystemctl_CannotStretchTheWaitBeyondTheBudget()
    {
        using var harness = new Harness(
            probe: new ScriptedProbe(),
            resolver: new FakeResolver(IPAddress.Loopback),
            processes: new HangingProcess(),
            timings: new UiTimings(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10)));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.UpstreamUnavailable);
        harness.Output.ToString().ShouldContain(Remedy);
        harness.Process.Calls.ShouldHaveSingleItem().File.ShouldBe("systemctl");
    }

    [Fact]
    public async Task MissingSystemctl_FailsAsConsoleUnreachableNotAsARuntimeCrash()
    {
        using var harness = new Harness(
            probe: new ScriptedProbe(),
            resolver: new FakeResolver(IPAddress.Loopback),
            processes: new ThrowingProcess(),
            timings: new UiTimings(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(10)));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.UpstreamUnavailable);
        harness.Output.ToString().ShouldContain("error [console_unreachable]");
    }

    [Theory]
    [InlineData(true, false, "cmd")]
    [InlineData(false, true, "open")]
    [InlineData(false, false, "xdg-open")]
    public void BrowserCommand_PicksThePlatformLauncher(bool windows, bool macOS, string expected)
    {
        var launch = UiCommand.BrowserCommand(PrettyUrl, windows, macOS);

        launch.File.ShouldBe(expected);
        launch.Args.ShouldContain(PrettyUrl);
        if (windows)
        {
            launch.Args.ShouldContain(string.Empty);
        }
    }

    [Fact]
    public async Task BrowserLauncherRefused_ReportsRuntimeAndHandsOverTheUrl()
    {
        using var harness = new Harness(processes: new FakeProcess(browserExitCode: 3));

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Runtime);
        var output = harness.Output.ToString();
        output.ShouldContain("error [browser_unavailable]");
        output.ShouldContain(PrettyUrl);
    }

    [Fact]
    public async Task EnableAtLogin_WritesTheEntryAndNeverStartsOrOpensTheConsole()
    {
        using var harness = new Harness();

        var code = await harness.RunAsync("ui", "--enable-at-login");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        var entry = await File.ReadAllTextAsync(harness.LoginPath);
        entry.ShouldContain("Type=Application");
        entry.ShouldContain("Name=Grimoire");
        entry.ShouldContain("X-GNOME-Autostart-enabled=true");
        entry.ShouldContain($"Exec={LoginBinary} ui");
        harness.Probe.Calls.ShouldBe(0);
        harness.Process.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnableAtLogin_AgainReportsUpToDateAndKeepsTheEntryBytes()
    {
        using var harness = new Harness();
        await harness.RunAsync("ui", "--enable-at-login");
        var written = await File.ReadAllBytesAsync(harness.LoginPath);

        var code = await harness.RunAsync("ui", "--enable-at-login");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        harness.Output.ToString().ShouldContain("already up to date");
        (await File.ReadAllBytesAsync(harness.LoginPath)).ShouldBe(written);
    }

    [Fact]
    public async Task DisableAtLogin_RemovesTheEntryThatEnableWrote()
    {
        using var harness = new Harness();
        await harness.RunAsync("ui", "--enable-at-login");

        var code = await harness.RunAsync("ui", "--disable-at-login");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        File.Exists(harness.LoginPath).ShouldBeFalse();
        harness.Output.ToString().ShouldContain("removed");
    }

    [Fact]
    public async Task DisableAtLogin_WhenNoEntryWasEverWritten_ReportsPlainlyAndExitsZero()
    {
        using var harness = new Harness();

        var code = await harness.RunAsync("ui", "--disable-at-login");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        harness.Output.ToString().ShouldContain("No login entry at");
        Directory.Exists(harness.LoginDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task LoginFlagsTogether_FailAsUsageBeforeAnyFileWrite()
    {
        using var harness = new Harness();

        var code = await harness.RunAsync("ui", "--enable-at-login", "--disable-at-login");

        code.ShouldBe(ExitCodes.Usage);
        var output = harness.Output.ToString();
        output.ShouldContain("error [usage]");
        output.ShouldContain("contradict each other");
        Directory.Exists(harness.LoginDirectory).ShouldBeFalse();
        harness.Probe.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PlainUi_PerformsNoLoginEntryAccess()
    {
        var spy = new SpyAutostart(Path.Combine(Path.GetTempPath(), "grimoire-ui-spy", AutostartEntryWriter.FileName));
        using var harness = new Harness(loginEntry: spy);

        var code = await harness.RunAsync("ui");

        code.ShouldBe(ExitCodes.Ok, harness.Output.ToString());
        spy.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void LoginEntry_ResolvesFromTheProductionContainer()
    {
        using var provider = CliServices.Build(new GrimoireOptions());

        provider.GetRequiredService<IAutostartEntryWriter>().ShouldBeOfType<AutostartEntryWriter>();
    }

    private sealed class SpyAutostart(string path) : IAutostartEntryWriter
    {
        public List<string> Calls { get; } = [];

        public string EntryPath
        {
            get
            {
                Calls.Add("path");
                return path;
            }
        }

        public Task<AutostartEntryChange> EnableAsync(CancellationToken cancellationToken)
        {
            Calls.Add("enable");
            return Task.FromResult(AutostartEntryChange.Written);
        }

        public Task<AutostartEntryChange> DisableAsync(CancellationToken cancellationToken)
        {
            Calls.Add("disable");
            return Task.FromResult(AutostartEntryChange.Removed);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _services;

        private readonly string _loginDirectory = Path.Combine(Path.GetTempPath(), "grimoire-ui-login", Guid.NewGuid().ToString("N"));

        public Harness(
            IConsoleProbe? probe = null,
            IHostNameResolver? resolver = null,
            IExternalProcess? processes = null,
            UiTimings? timings = null,
            int port = 5099,
            IAutostartEntryWriter? loginEntry = null)
        {
            Process = processes as RecordingProcess ?? new FakeProcess();
            Probe = probe as ScriptedProbe ?? new ScriptedProbe(alwaysAlive: true);
            Resolver = resolver as FakeResolver ?? new FakeResolver(IPAddress.Loopback);
            Output = new StringWriter();
            Login = loginEntry ?? new AutostartEntryWriter(
                Path.Combine(_loginDirectory, AutostartEntryWriter.FileName),
                LoginBinary);

            var services = new ServiceCollection();
            services.AddSingleton(probe ?? Probe);
            services.AddSingleton<IExternalProcess>(processes ?? Process);
            services.AddSingleton(resolver ?? Resolver);
            services.AddSingleton(loginEntry ?? Login);
            services.AddSingleton(timings ?? new UiTimings(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(10)));
            _services = services.BuildServiceProvider();

            var configPath = Path.Combine(Path.GetTempPath(), "grimoire-ui-absent", "config.json");
            Env = new CliEnvironment
            {
                ConfigPath = configPath,
                Config = new ConfigLoader(
                    configPath,
                    new Dictionary<string, string> { ["GRIMOIRE_SERVE__PORT"] = port.ToString() }),
                Output = Output,
                Error = Output,
                Services = _services,
            };
        }

        public RecordingProcess Process { get; }

        public ScriptedProbe Probe { get; }

        public FakeResolver Resolver { get; }

        public StringWriter Output { get; }

        public CliEnvironment Env { get; }

        public IAutostartEntryWriter Login { get; }

        public string LoginDirectory => _loginDirectory;

        public string LoginPath => Path.Combine(_loginDirectory, AutostartEntryWriter.FileName);

        public Task<int> RunAsync(params string[] args) => CliApplication.RunAsync(args, Env);

        public void Dispose()
        {
            _services.Dispose();
            if (Directory.Exists(_loginDirectory))
            {
                Directory.Delete(_loginDirectory, true);
            }
        }
    }

    internal record Spawn(string File, IReadOnlyList<string> Args);

    internal abstract class RecordingProcess : IExternalProcess
    {
        public List<Spawn> Calls { get; } = [];

        public abstract Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
    }

    internal sealed class FakeProcess(int browserExitCode = 0) : RecordingProcess
    {
        public override Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add(new Spawn(file, arguments));
            return Task.FromResult(file == "systemctl" ? 0 : browserExitCode);
        }
    }

    private sealed class HangingProcess : RecordingProcess
    {
        public override async Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add(new Spawn(file, arguments));
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class ThrowingProcess : RecordingProcess
    {
        public override Task<int> RunAsync(string file, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add(new Spawn(file, arguments));
            throw new ProcessSpawnException(file, "systemctl not found on PATH");
        }
    }

    internal sealed class ScriptedProbe(bool alwaysAlive = false, int aliveFromCall = 0) : IConsoleProbe
    {
        public int Calls { get; private set; }

        public Task<bool> IsAliveAsync(int port, CancellationToken cancellationToken)
        {
            Calls++;
            var alive = alwaysAlive || (aliveFromCall > 0 && Calls >= aliveFromCall);
            return Task.FromResult(alive);
        }
    }

    private sealed class CountingProbe(IConsoleProbe inner) : IConsoleProbe
    {
        public int Attempts { get; private set; }

        public async Task<bool> IsAliveAsync(int port, CancellationToken cancellationToken)
        {
            Attempts++;
            return await inner.IsAliveAsync(port, cancellationToken);
        }
    }

    internal sealed class FakeResolver(params IPAddress[] addresses) : IHostNameResolver
    {
        public List<string> Lookups { get; } = [];

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Lookups.Add(host);
            return Task.FromResult<IReadOnlyList<IPAddress>>(addresses);
        }
    }

    // Accepts the connection, then says nothing back: the socket a probe deadline has to survive.
    private sealed class HungListener : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _stop = new();

        public HungListener()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(AcceptAndParkAsync);
        }

        public int Port { get; }

        private async Task AcceptAndParkAsync()
        {
            var parked = new List<TcpClient>();
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    parked.Add(await _listener.AcceptTcpClientAsync(_stop.Token));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            foreach (var client in parked)
            {
                client.Dispose();
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }
    }

    // Answers /health on the loopback literal so the adapter under test is the real one.
    private sealed class StubConsole : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _stop = new();

        public StubConsole(HttpStatusCode status)
        {
            Port = FreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                while (!_stop.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }

                    RequestPaths.Add(context.Request.Url?.AbsolutePath ?? string.Empty);
                    LastHostHeader = context.Request.Headers["Host"] ?? string.Empty;
                    context.Response.StatusCode = (int)status;
                    context.Response.Close();
                }
            });
        }

        public int Port { get; }

        public List<string> RequestPaths { get; } = [];

        public string LastHostHeader { get; private set; } = string.Empty;

        public HttpClient Client() => new();

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Close();
            _stop.Dispose();
        }
    }
}
