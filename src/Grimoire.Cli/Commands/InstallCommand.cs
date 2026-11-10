using Grimoire.Cli.CommandLine;
using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Cli.Commands;

internal sealed class InstallCommand(CliEnvironment environment)
{
    public Task<int> RunAsync(
        CliInvocation invocation,
        GrimoireOptions config,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var remove = CliValues.Switch(invocation, invocation.Spec, "remove");
        var printBlock = CliValues.Switch(invocation, invocation.Spec, "print-block");
        if (remove && printBlock)
        {
            throw new UsageException("--remove and --print-block are mutually exclusive.", invocation.Spec);
        }

        if (printBlock)
        {
            environment.Output.WriteLine(InstallBlock.Text);
            return Task.FromResult(ExitCodes.Ok);
        }

        var homeRoot = environment.Variable("GRIMOIRE_HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var path in AgentHomeMemoryFiles.Locate(homeRoot))
        {
            var outcome = remove
                ? InstallBlockFileWriter.Remove(path)
                : InstallBlockFileWriter.Upsert(path, InstallBlock.Text);
            environment.Output.WriteLine($"{path}: {Describe(outcome)}");
        }

        return Task.FromResult(ExitCodes.Ok);
    }

    private static string Describe(InstallBlockOutcome outcome) => outcome switch
    {
        InstallBlockOutcome.Created => "created",
        InstallBlockOutcome.Updated => "updated",
        InstallBlockOutcome.Unchanged => "unchanged",
        InstallBlockOutcome.Removed => "removed",
        _ => "skipped (not found)",
    };
}
