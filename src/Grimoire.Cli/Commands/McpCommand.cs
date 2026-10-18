using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Mcp;

namespace Grimoire.Cli.Commands;

internal sealed class McpCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var options = BuildHostOptions(
            ActorResolver.TryResolve(environment, invocation.Value("actor")),
            environment.Variable(McpHostOptions.TomeEnvironmentVariable),
            environment.Variable(ActorResolver.EnvironmentVariable));
        environment.Error.WriteLine($"Grimoire MCP server '{options.ServerName}' v{options.ServerVersion} listening on stdio");
        return await McpHost.RunAsync(options, services, cancellationToken);
    }

    internal static McpHostOptions BuildHostOptions(MemoryActor? actor, string? envTome = null, string? envActor = null) => new()
    {
        ServerVersion = VersionText(),
        Actor = actor,
        EnvTome = envTome,
        EnvActor = envActor,
    };

    private static string VersionText() =>
        typeof(McpCommand).Assembly.GetName().Version is { } version ? version.ToString(3) : "0.1.0";
}
