using System.Globalization;
using Grimoire.Api;
using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Grimoire.Mcp;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class ServeCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await services.GetRequiredService<QdrantCollectionBootstrapper>().EnsureAsync(
            EmbeddingFingerprint.From(config.Embedding.Model, config.Embedding.Dimensions),
            cancellationToken);

        var options = BuildHostOptions(
            config,
            invocation.Value("web-root"),
            environment.Variable(McpHostOptions.TomeEnvironmentVariable),
            environment.Variable(ActorResolver.EnvironmentVariable),
            services.GetService<IAgentIdentitySource>());
        environment.Output.WriteLine($"Serving http://127.0.0.1:{options.Port} (static root: {options.StaticRootPath})");
        // The Api host logs the same verdict structurally; this line is the operator's copy of it.
        WriteStampWarning(environment.Output, SpaStampVerifier.Read(options.StaticRootPath));
        return await ApiHost.RunAsync(options, services, cancellationToken);
    }

    internal static void WriteStampWarning(TextWriter output, SpaStampVerdict verdict)
    {
        if (verdict.WarningLine is { } warning)
        {
            output.WriteLine($"Warning: {warning}");
        }
    }

    internal static ApiHostOptions BuildHostOptions(
        GrimoireOptions config,
        string? webRoot,
        string? envTome = null,
        string? envActor = null,
        IAgentIdentitySource? identityFile = null) => new()
        {
            Port = config.Serve.Port,
            GcIntervalMinutes = Math.Max(1, (int)Math.Round(config.Serve.GcIntervalMinutes)),
            SessionsRetentionHours = RequireUsableRetention(config),
            StaticRootPath = webRoot ?? DefaultWebRoot(),
            EnvTome = envTome,
            EnvActor = envActor,
            IdentityFile = identityFile,
        };

    // The window is the one number the serve-side retention sweep subtracts from the clock.
    // A zero or negative value turns the cutoff into now or the future and retires live
    // handshake rows, so an unusable knob stops serve at startup instead of eating the registry.
    private static double RequireUsableRetention(GrimoireOptions config)
    {
        var hours = config.SessionsRetentionHoursOrDefault;
        if (!double.IsFinite(hours) || hours <= 0)
        {
            throw new ConfigException(
                $"Session registry retention must be a positive finite number of hours, but sessions.retentionHours " +
                $"(or its memory.sessionTtlHours fallback) resolves to '{hours.ToString(CultureInfo.InvariantCulture)}'.");
        }

        return hours;
    }

    internal static string DefaultWebRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "web");
}
