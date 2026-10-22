using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Grimoire.Cli;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args, CliEnvironment? environment = null, CancellationToken cancellationToken = default)
    {
        var env = environment ?? CliEnvironment.Default();

        try
        {
            if (TryWriteHelp(args, env))
            {
                return ExitCodes.Ok;
            }

            var invocation = CommandLineParser.Parse(args);
            var config = env.Config.Load();
            var ownsServices = env.Services is null;
            var services = env.Services ?? CliServices.Build(config);
            using var cancellation = LinkCancellation(cancellationToken, out var detach);
            try
            {
                return await DispatchAsync(invocation, env, config, services, cancellation.Token);
            }
            finally
            {
                detach();
                if (ownsServices && services is ServiceProvider provider)
                {
                    await provider.DisposeAsync();
                }
            }
        }
        catch (UsageException ex)
        {
            Report(env, "usage", ex.Message);
            UsageWriter.WriteRecovery(env.Output, ex.Spec);
            return ExitCodes.Usage;
        }
        catch (CommandException ex)
        {
            Report(env, ex.Code, ex.Message);
            return ex.ExitCode;
        }
        catch (ConfigException ex)
        {
            Report(env, "config", ex.Message);
            return ExitCodes.Config;
        }
        catch (UpstreamUnavailableException ex)
        {
            Report(env, "upstream_unavailable", UnavailableMessage(ex.Upstream));
            return ExitCodes.UpstreamUnavailable;
        }
        catch (ReindexRequiredException ex)
        {
            Report(env, "reindex_required", ex.Message);
            return ExitCodes.ReindexRequired;
        }
        catch (Exception ex) when (ex is EmbeddingFingerprintException or RpcException)
        {
            Report(env, "dependency", ex.Message);
            return ExitCodes.Dependency;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Ok;
        }
        catch (Exception ex)
        {
            Report(env, "runtime", ex.Message);
            Log.Error(ex, "Command {Args} failed", args);
            return ExitCodes.Runtime;
        }
    }

    private static Task<int> DispatchAsync(
        CliInvocation invocation,
        CliEnvironment env,
        Grimoire.Infrastructure.Configuration.GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
        invocation.Spec.Name switch
        {
            "init" => new Commands.InitCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "serve" => new Commands.ServeCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "ui" => new Commands.UiCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "mcp" => new Commands.McpCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "remember" => new Commands.RememberCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "recall" => new Commands.RecallCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "list" => new Commands.ListCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "brief" => new Commands.BriefCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "scry" => new Commands.ScryCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "forget" => new Commands.ForgetCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "gc" => new Commands.GcCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "reindex" => new Commands.ReindexCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "export" => new Commands.ExportCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "import" => new Commands.ImportCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "persona" => new Commands.PersonaCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "stats" => new Commands.StatsCommand(env).RunAsync(invocation, config, services, cancellationToken),
            "config" => new Commands.ConfigCommand(env).RunAsync(invocation, config, services, cancellationToken),
            _ => throw new UsageException($"Unknown command '{invocation.Spec.Name}'."),
        };

    private static bool TryWriteHelp(string[] args, CliEnvironment env)
    {
        if (args.Length == 0)
        {
            UsageWriter.WriteGeneral(env.Output);
            return true;
        }

        if (args[0] is "help" or "--help" or "-h")
        {
            if (args.Length > 1 && CommandCatalog.Find(args[1]) is { } topic)
            {
                UsageWriter.WriteCommand(env.Output, topic);
            }
            else
            {
                UsageWriter.WriteGeneral(env.Output);
            }

            return true;
        }

        if (CommandCatalog.Find(args[0]) is { } spec && args.Skip(1).Any(IsHelpFlag))
        {
            UsageWriter.WriteCommand(env.Output, spec);
            return true;
        }

        return false;
    }

    private static bool IsHelpFlag(string value) => value is "--help" or "-h";

    private static CancellationTokenSource LinkCancellation(CancellationToken cancellationToken, out Action detach)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            source.Cancel();
        };

        Console.CancelKeyPress += handler;
        detach = () => Console.CancelKeyPress -= handler;
        return source;
    }

    private static void Report(CliEnvironment env, string code, string message) =>
        env.Error.WriteLine($"error [{code}]: {message}");

    private static string UnavailableMessage(string upstream) =>
        $"Upstream '{upstream}' is unavailable: not reachable, it timed out, or it rejected the configured credentials. " +
        "Nothing was written by this call. Check the service and its configuration, then retry.";
}
