using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class PersonaCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var persona = services.GetRequiredService<PersonaService>();
        return (invocation.Positional.FirstOrDefault() ?? "show") switch
        {
            "show" => await ShowAsync(persona, cancellationToken),
            "set-section" => await SetSectionAsync(persona, invocation, services, cancellationToken),
            var action => throw new UsageException(
                $"Unknown persona action '{action}' (expected: show, set-section).", invocation.Spec),
        };
    }

    private async Task<int> ShowAsync(PersonaService persona, CancellationToken cancellationToken)
    {
        var document = await persona.GetAsync(cancellationToken);
        foreach (var section in PersonaSectionExtensions.Sections)
        {
            environment.Output.WriteLine($"{section.ToString().ToLowerInvariant()}:");
            var content = document.Read(section);
            environment.Output.WriteLine(content.Length == 0 ? "  (empty)" : Indent(content));
        }

        return ExitCodes.Ok;
    }

    private async Task<int> SetSectionAsync(
        PersonaService persona,
        CliInvocation invocation,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (invocation.Positional.Count < 3)
        {
            throw new UsageException("Usage: grimoire persona set-section <identity|tone|preferences|rules> <text>", invocation.Spec);
        }

        var section = CliValues.Section(invocation.Spec, invocation.Positional[1]);
        var text = string.Join(" ", invocation.Positional.Skip(2));
        var actor = ActorResolver.Resolve(environment, invocation.Value("actor"));
        var current = await persona.GetAsync(cancellationToken);
        await persona.SetAsync(current.WithSection(section, text), actor, cancellationToken);
        environment.Output.WriteLine($"Persona section '{section.ToString().ToLowerInvariant()}' set by {actor}");
        return ExitCodes.Ok;
    }

    private static string Indent(string content) => string.Join(
        "\n",
        content.Split('\n').Select(line => $"  {line.TrimEnd('\r')}"));
}
