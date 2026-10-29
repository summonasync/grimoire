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
        return await ApiHost.RunAsync(options, services, cancellationToken);
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
            StaticRootPath = webRoot ?? DefaultWebRoot(),
            EnvTome = envTome,
            EnvActor = envActor,
            IdentityFile = identityFile,
        };

    internal static string DefaultWebRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "web");
}
