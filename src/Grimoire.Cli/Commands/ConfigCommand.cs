using System.Text.Json;
using System.Text.Json.Nodes;
using Grimoire.Cli.CommandLine;
using Grimoire.Cli.Configuration;
using Grimoire.Infrastructure.Configuration;

namespace Grimoire.Cli.Commands;

internal sealed class ConfigCommand(CliEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(invocation.Positional.FirstOrDefault() switch
        {
            null or "get" => Show(config),
            "path" => ShowPath(),
            "set" => Set(invocation),
            var action => throw new UsageException(
                $"Unknown config action '{action}' (expected: get, set, path).", invocation.Spec),
        });
    }

    private int Show(GrimoireOptions config)
    {
        environment.Output.WriteLine(JsonSerializer.Serialize(config, JsonOutput));
        return ExitCodes.Ok;
    }

    private int ShowPath()
    {
        environment.Output.WriteLine(environment.ConfigPath);
        return ExitCodes.Ok;
    }

    private int Set(CliInvocation invocation)
    {
        if (invocation.Positional.Count < 3)
        {
            throw new UsageException("Usage: grimoire config set <section.property> <value>", invocation.Spec);
        }

        var key = invocation.Positional[1];
        var value = string.Join(" ", invocation.Positional.Skip(2));
        var path = environment.ConfigPath;
        var (previous, existed) = ConfigFile.Read(path);
        var schema = ConfigFile.DefaultsRoot();
        var root = existed ? ParseRoot(previous!, path) : schema;

        ConfigFile.ApplyValue(root, schema, key, value);
        ConfigFile.Write(path, root.ToJsonString(JsonOutput));
        try
        {
            _ = new ConfigLoader(path).Load();
        }
        catch (ConfigException ex)
        {
            if (existed)
            {
                ConfigFile.Write(path, previous!);
            }
            else
            {
                File.Delete(path);
            }

            throw new CommandException("config", ex.Message, ExitCodes.Config);
        }

        environment.Output.WriteLine($"Set {key} = {value}");
        return ExitCodes.Ok;
    }

    private static JsonObject ParseRoot(string text, string path)
    {
        if (JsonNode.Parse(text) is not JsonObject root)
        {
            throw new CommandException("config", $"Config file '{path}' must contain a JSON object at the root.", ExitCodes.Config);
        }

        return root;
    }
}
