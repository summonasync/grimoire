using Grimoire.Core;

namespace Grimoire.Api.Tests.Fakes;

internal sealed class FixedCallerPath(string path) : ICallerPathProvider
{
    public string Path { get; } = path;
}
