using Grimoire.Api;
using Grimoire.Cli.CommandLine;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Mcp;

namespace Grimoire.Cli.Commands;

internal sealed class ServeCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var options = BuildHostOptions(
            config,
            invocation.Value("web-root"),
            environment.Variable(McpHostOptions.TomeEnvironmentVariable),
            environment.Variable(ActorResolver.EnvironmentVariable));
        environment.Output.WriteLine($"Serving http://127.0.0.1:{options.Port} (static root: {options.StaticRootPath})");
        return await ApiHost.RunAsync(options, services, cancellationToken);
    }

    internal static ApiHostOptions BuildHostOptions(GrimoireOptions config, string? webRoot, string? envTome = null, string? envActor = null) => new()
    {
        Port = config.Serve.Port,
        GcIntervalMinutes = Math.Max(1, (int)Math.Round(config.Serve.GcIntervalMinutes)),
        StaticRootPath = webRoot ?? DefaultWebRoot(),
        EnvTome = envTome,
        EnvActor = envActor,
    };

    internal static string DefaultWebRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".grimoire",
        "web");
}
