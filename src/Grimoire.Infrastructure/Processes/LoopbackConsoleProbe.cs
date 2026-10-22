using Grimoire.Core;

namespace Grimoire.Infrastructure.Processes;

public sealed class LoopbackConsoleProbe(HttpClient http) : IConsoleProbe
{
    // IPv4 literal on purpose: the HTTP face binds IPAddress.Loopback only, while
    // grimoire.localhost resolves to ::1 too — a hostname probe can hang on an address
    // the product never binds.
    public async Task<bool> IsAliveAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
