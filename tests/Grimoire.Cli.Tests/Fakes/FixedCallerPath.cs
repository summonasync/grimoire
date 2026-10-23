using Grimoire.Core;

namespace Grimoire.Cli.Tests.Fakes;

internal sealed class FixedCallerPath(string path) : ICallerPathProvider
{
    public string Path { get; } = path;
}
