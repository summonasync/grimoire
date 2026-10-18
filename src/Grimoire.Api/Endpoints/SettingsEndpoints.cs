using System.Net;
using Grimoire.Api.Contracts;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app, MemoryEngineOptions engineOptions, ApiHostOptions hostOptions)
    {
        app.MapGet("/api/settings", () => Results.Ok(new SettingsResponse(
            new EngineSettings(
                engineOptions.SessionTtlHours,
                engineOptions.DedupSimilarityThreshold,
                engineOptions.TombstoneRetentionDays,
                engineOptions.RecallOversampling,
                engineOptions.RecencyHalfLives,
                engineOptions.Compaction),
            new ServeSettings(hostOptions.Port, hostOptions.GcIntervalMinutes),
            SettingsPersistence.Deferred,
            UpdateSettingsRequest.EditableKeys)));

        app.MapPut("/api/settings", (UpdateSettingsRequest request) => ApiResponses.Error(
            HttpStatusCode.NotImplemented,
            "settings_persistence_not_implemented",
            $"Settings persistence is not available in v1. {SettingsPersistence.Deferred.Hint}"));
    }
}
