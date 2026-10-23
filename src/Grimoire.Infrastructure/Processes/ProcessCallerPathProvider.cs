using Grimoire.Core;

namespace Grimoire.Infrastructure.Processes;

public sealed class ProcessCallerPathProvider : ICallerPathProvider
{
    public string Path => Environment.CurrentDirectory;
}
