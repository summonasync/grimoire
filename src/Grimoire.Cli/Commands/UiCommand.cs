using System.Diagnostics;
using System.Net;
using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Autostart;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class UiCommand(CliEnvironment environment)
{
    internal const string PrettyHost = "grimoire.localhost";
    internal const string LoopbackHost = "127.0.0.1";
    internal const string ServeUnit = "grimoire-serve";
    internal const string RemedyCommand = "systemctl --user start grimoire-serve";
    internal const string EnableAtLoginFlag = "enable-at-login";
    internal const string DisableAtLoginFlag = "disable-at-login";

    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // The login flags are an admin mode: they touch the session entry and leave. Plain `ui` never reaches them.
        var enable = CliValues.Switch(invocation, invocation.Spec, EnableAtLoginFlag);
        var disable = CliValues.Switch(invocation, invocation.Spec, DisableAtLoginFlag);
        if (enable && disable)
        {
            throw new UsageException($"--{EnableAtLoginFlag} and --{DisableAtLoginFlag} contradict each other; pick one.", invocation.Spec);
        }

        if (enable || disable)
        {
            return await ManageLoginEntryAsync(services, enable, cancellationToken);
        }

        var port = config.Serve.Port;
        var timings = services.GetService<UiTimings>() ?? UiTimings.Default;
        var probe = services.GetRequiredService<IConsoleProbe>();
        var processes = services.GetRequiredService<IExternalProcess>();
        var resolver = services.GetRequiredService<IHostNameResolver>();

        if (!await AnswersAsync(probe, port, timings.AttemptDeadline, cancellationToken)
            && !await RaiseConsoleAsync(probe, processes, port, timings, cancellationToken))
        {
            throw Unreachable(port, $"no answer within {Format(timings.Budget)} even after {RemedyCommand}");
        }

        var url = await ConsoleUrlAsync(resolver, port, timings, cancellationToken);
        environment.Output.WriteLine($"Console at {url} (health answered on {LoopbackHost}:{port})");
        return await OpenBrowserAsync(processes, url, cancellationToken);
    }

    private async Task<int> ManageLoginEntryAsync(
        IServiceProvider services,
        bool enable,
        CancellationToken caller)
    {
        var entry = services.GetRequiredService<IAutostartEntryWriter>();
        var change = enable
            ? await entry.EnableAsync(caller)
            : await entry.DisableAsync(caller);

        environment.Output.WriteLine(LoginLine(change, entry.EntryPath));
        return ExitCodes.Ok;
    }

    private static string LoginLine(AutostartEntryChange change, string path) => change switch
    {
        AutostartEntryChange.Written => $"Login entry written to {path}",
        AutostartEntryChange.UpToDate => $"Login entry at {path} is already up to date",
        AutostartEntryChange.Removed => $"Login entry removed from {path}",
        _ => $"No login entry at {path} to remove",
    };

    // One start only: systemd owns the unit lifecycle and a retry loop races it. The budget clock
    // covers the start and the poll together, so a wedged systemctl cannot stretch the wait.
    private async Task<bool> RaiseConsoleAsync(
        IConsoleProbe probe,
        IExternalProcess processes,
        int port,
        UiTimings timings,
        CancellationToken caller)
    {
        var budget = Stopwatch.StartNew();
        try
        {
            using var window = CancellationTokenSource.CreateLinkedTokenSource(caller);
            window.CancelAfter(timings.Budget);
            var exitCode = await processes.RunAsync("systemctl", ["--user", "start", ServeUnit], window.Token);
            if (exitCode != 0)
            {
                throw Unreachable(port, $"'{RemedyCommand}' exited {exitCode}");
            }
        }
        catch (ProcessSpawnException spawn)
        {
            throw Unreachable(port, $"'{spawn.File}' is not available to start the unit");
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            throw Unreachable(port, $"'{RemedyCommand}' never returned within {Format(timings.Budget)}");
        }

        while (true)
        {
            var left = timings.Budget - budget.Elapsed;
            if (left <= TimeSpan.Zero)
            {
                return false;
            }

            var attempt = timings.AttemptDeadline < left ? timings.AttemptDeadline : left;
            if (await AnswersAsync(probe, port, attempt, caller))
            {
                return true;
            }

            var pause = timings.PollInterval < left ? timings.PollInterval : left;
            if (pause > TimeSpan.Zero)
            {
                await Task.Delay(pause, caller);
            }
        }
    }

    // Each probe carries its own deadline: the shared HttpClient would otherwise sit on a
    // listening-but-silent socket for its 100s default and swallow the whole budget in one attempt.
    private async Task<bool> AnswersAsync(IConsoleProbe probe, int port, TimeSpan deadline, CancellationToken caller)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(caller);
        attempt.CancelAfter(deadline);
        try
        {
            return await probe.IsAliveAsync(port, attempt.Token);
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<string> ConsoleUrlAsync(
        IHostNameResolver resolver,
        int port,
        UiTimings timings,
        CancellationToken caller)
    {
        var plain = $"http://{LoopbackHost}:{port}";
        using var window = CancellationTokenSource.CreateLinkedTokenSource(caller);
        window.CancelAfter(timings.AttemptDeadline);
        try
        {
            var addresses = await resolver.ResolveAsync(PrettyHost, window.Token);
            return addresses.Any(IPAddress.IsLoopback) ? $"http://{PrettyHost}:{port}" : plain;
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            return plain;
        }
    }

    private async Task<int> OpenBrowserAsync(IExternalProcess processes, string url, CancellationToken caller)
    {
        var (file, args) = BrowserCommand(
            url,
            windows: OperatingSystem.IsWindows(),
            macOS: OperatingSystem.IsMacOS());
        try
        {
            var exitCode = await processes.RunAsync(file, args, caller);
            if (exitCode != 0)
            {
                throw new CommandException(
                    "browser_unavailable",
                    $"'{file}' exited {exitCode} without opening the console. Open {url} yourself.",
                    ExitCodes.Runtime);
            }
        }
        catch (ProcessSpawnException spawn)
        {
            throw new CommandException(
                "browser_unavailable",
                $"'{spawn.File}' is not available to open {url} ({spawn.Message})",
                ExitCodes.Runtime);
        }

        return ExitCodes.Ok;
    }

    internal static (string File, IReadOnlyList<string> Args) BrowserCommand(string url, bool windows, bool macOS) => (windows, macOS) switch
    {
        // 'start' reads the first quoted argument as a window title, so it gets an empty one.
        (true, _) => ("cmd", ["/c", "start", "", url]),
        (_, true) => ("open", [url]),
        _ => ("xdg-open", [url]),
    };

    private static string Format(TimeSpan span) => $"{span.TotalSeconds:0.##}s";

    private CommandException Unreachable(int port, string detail) => new(
        "console_unreachable",
        $"The console never answered on http://{LoopbackHost}:{port}/health ({detail}). Remedy: {RemedyCommand}",
        ExitCodes.UpstreamUnavailable);
}
