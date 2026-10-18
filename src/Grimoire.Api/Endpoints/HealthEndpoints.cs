using Grimoire.Api.Contracts;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app, IMemoryStore store, IEmbeddingProvider embeddings, IClock clock)
    {
        app.MapGet("/health", async (CancellationToken cancellationToken) =>
        {
            var storeHealth = await ProbeAsync(() => store.CountAsync(new MemoryFilter(), cancellationToken), cancellationToken);
            var embeddingHealth = await ProbeAsync(() => ProbeEmbeddingsAsync(embeddings, cancellationToken), cancellationToken);
            var status = storeHealth.Reachable && embeddingHealth.Reachable
                ? HealthResponse.Healthy
                : HealthResponse.Degraded;

            return Results.Ok(new HealthResponse(status, storeHealth, embeddingHealth, clock.UtcNow));
        });
    }

    private static async Task<ComponentHealth> ProbeAsync<T>(Func<Task<T>> probe, CancellationToken cancellationToken)
    {
        try
        {
            await probe();
            return ComponentHealth.Up;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ComponentHealth.Down(ex.Message);
        }
    }

    private static async Task<bool> ProbeEmbeddingsAsync(IEmbeddingProvider embeddings, CancellationToken cancellationToken)
    {
        _ = await embeddings.EmbedAsync("grimoire-health-probe", cancellationToken);
        return true;
    }
}
