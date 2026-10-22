using System.Net;

namespace Grimoire.Core;

public interface IHostNameResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}
