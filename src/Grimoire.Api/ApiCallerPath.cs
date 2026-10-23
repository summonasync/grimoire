using Grimoire.Core;

namespace Grimoire.Api;

internal sealed class ApiCallerPath : ICallerPathProvider
{
    public string Path => Environment.CurrentDirectory;
}
