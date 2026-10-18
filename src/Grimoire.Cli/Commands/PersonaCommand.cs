using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class PersonaCommand(CliEnvironment environment)
{
    public Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        return (invocation.Positional.FirstOrDefault() ?? "show") switch
        {
            "show" => ShowAsync(invocation, config, services, cancellationToken),
            "set-section" => SetSectionAsync(invocation, config, services, cancellationToken),
            var action => throw new UsageException(
                $"Unknown persona action '{action}' (expected: show, set-section).", invocation.Spec),
        };
    }

    private async Task<int> ShowAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var identity = await ResolveIdentityAsync(invocation, config, services, cancellationToken);
        var persona = services.GetRequiredService<PersonaService>();
        var document = await persona.GetAsync(cancellationToken, identity.Tome);
        environment.Output.WriteLine($"Tome: {identity.Tome}");
        foreach (var section in PersonaSectionExtensions.Sections)
        {
            environment.Output.WriteLine($"{section.ToString().ToLowerInvariant()}:");
            var content = document.Read(section);
            environment.Output.WriteLine(content.Length == 0 ? "  (empty)" : Indent(content));
        }

        return ExitCodes.Ok;
    }

    private async Task<int> SetSectionAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (invocation.Positional.Count < 3)
        {
            throw new UsageException("Usage: grimoire persona set-section <identity|tone|preferences|rules> <text>", invocation.Spec);
        }

        var section = CliValues.Section(invocation.Spec, invocation.Positional[1]);
        var text = string.Join(" ", invocation.Positional.Skip(2));
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UsageException("Persona section text must not be empty; clearing sections is not supported in v1.", invocation.Spec);
        }

        var identity = await ResolveIdentityAsync(invocation, config, services, cancellationToken);
        var persona = services.GetRequiredService<PersonaService>();
        var current = await persona.GetAsync(cancellationToken, identity.Tome);
        await persona.SetAsync(current.WithSection(section, text), identity.Actor, cancellationToken, identity.Tome);
        environment.Output.WriteLine(
            $"Persona section '{section.ToString().ToLowerInvariant()}' set by {identity.Actor} in tome '{identity.Tome}'");
        return ExitCodes.Ok;
    }

    private Task<CliIdentity> ResolveIdentityAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken) =>
        ActorResolver.ResolveAsync(
            environment,
            invocation.Value("actor"),
            invocation.Value("tome"),
            config.DefaultTome,
            services.GetRequiredService<IActorClaimProvider>(),
            cancellationToken);

    private static string Indent(string content) => string.Join(
        "\n",
        content.Split('\n').Select(line => $"  {line.TrimEnd('\r')}"));
}
