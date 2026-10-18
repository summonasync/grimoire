using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Cli;

internal sealed class CliEnvironment
{
    public static CliEnvironment Default() => new()
    {
        ConfigPath = ConfigLoader.DefaultPath,
        Config = new ConfigLoader(),
    };

    public required string ConfigPath { get; init; }

    public required ConfigLoader Config { get; init; }

    public TextWriter Output { get; init; } = Console.Out;

    public TextWriter Error { get; init; } = Console.Error;

    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    public IServiceProvider? Services { get; init; }

    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;

    public string? Variable(string name) =>
        Variables is null ? Environment.GetEnvironmentVariable(name) : Variables.TryGetValue(name, out var value) ? value : null;
}
