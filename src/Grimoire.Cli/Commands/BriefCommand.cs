using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class BriefCommand(CliEnvironment environment)
{
    public async Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var daily = CliValues.Switch(invocation, invocation.Spec, "daily");
        var format = CliValues.Format(invocation.Spec, invocation.Value("format"));
        var maxChars = Hint(invocation, "max-chars");
        var window = Window(invocation);
        var brief = maxChars is { } cap
            ? BriefBudget.WithMaxTotalChars(services, cap)
            : services.GetRequiredService<BriefService>();
        var result = await brief.BriefAsync(new BriefQuery
        {
            ProjectId = invocation.Value("project"),
            Scopes = CliValues.ScopeList(invocation.Spec, invocation.Value("scope")),
            FactsPerScope = Hint(invocation, "facts-per-scope"),
            JournalPerScope = Hint(invocation, "journal-per-scope"),
            TombstoneLimit = Hint(invocation, "tombstone-limit"),
            Daily = daily,
            Since = window.Since,
            Until = window.Until,
        }, cancellationToken);

        if (format == BriefFormat.Hook)
        {
            BriefHookRenderer.Write(invocation.Spec, environment, result, daily);
            return ExitCodes.Ok;
        }

        MemoryFormatter.WriteBrief(environment.Output, result, daily);
        return ExitCodes.Ok;
    }

    // Core lets an inverted window read empty; the CLI is where a caller learns about it, so the range
    // type parses both bounds and its ArgumentException becomes the usage error.
    private static CreatedAtRange Window(CliInvocation invocation)
    {
        try
        {
            return CreatedAtRange.Parse(invocation.Value("since"), invocation.Value("until"));
        }
        catch (ArgumentException ex) when (ex.ParamName is "since" or "until")
        {
            throw new UsageException(ex.Message, invocation.Spec);
        }
    }

    private static int? Hint(CliInvocation invocation, string name) =>
        invocation.Value(name) is { } raw ? CliValues.PositiveInt(invocation.Spec, name, raw) : null;
}
