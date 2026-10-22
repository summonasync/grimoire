namespace Grimoire.Cli.Commands;

internal sealed record UiTimings(TimeSpan Budget, TimeSpan AttemptDeadline, TimeSpan PollInterval)
{
    internal static UiTimings Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromMilliseconds(200));
}
