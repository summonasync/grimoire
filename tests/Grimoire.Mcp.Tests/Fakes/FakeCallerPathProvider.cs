using Grimoire.Core;

namespace Grimoire.Mcp.Tests;

internal sealed class FakeCallerPathProvider(string path) : ICallerPathProvider
{
    public string Path { get; } = path;
}
