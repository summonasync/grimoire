using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Api.Agents;
using Grimoire.Api.Editing;
using Grimoire.Api.Endpoints;
using Grimoire.Api.Querying;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Grimoire.Api;

public static class ApiHostBuilder
{
    public static WebApplication Build(ApiHostOptions options, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(services);

        var store = services.GetRequiredService<IMemoryStore>();
        var embeddings = services.GetRequiredService<IEmbeddingProvider>();
        var clock = services.GetRequiredService<IClock>();
        var engineOptions = services.GetService<MemoryEngineOptions>() ?? new MemoryEngineOptions();
        var gcOptions = services.GetService<GcOptions>()
            ?? new GcOptions { SweepInterval = TimeSpan.FromMinutes(options.GcIntervalMinutes) };
        var gcState = services.GetService<IGcStateStore>();
        var memories = services.GetService<MemoryService>()
            ?? new MemoryService(store, embeddings, clock, engineOptions, gcOptions, gcState);
        var persona = services.GetService<PersonaService>() ?? new PersonaService(store, embeddings, clock);

        var builder = WebApplication.CreateBuilder();
        // Kestrel loads endpoint configuration (env vars, appsettings) additively on top of
        // code-level Listen; clearing the sources keeps ApiHostOptions the only bind authority.
        if (!options.DisableConfigSourceClear)
        {
            builder.Configuration.Sources.Clear();
        }
        builder.WebHost.UseKestrel(kestrel => LoopbackBinding.Configure(kestrel, options.Port));
        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });
        builder.Services.AddHostedService<LoopbackGuardService>();
        builder.Services.AddHostedService(_ => new GcSweeper(
            memories.GcAsync,
            clock,
            TimeSpan.FromMinutes(options.GcIntervalMinutes),
            gcState));

        var tomeResolver = new ApiTomeResolver(
            new TomeResolver(engineOptions),
            services.GetService<IActorClaimProvider>(),
            options.EnvTome,
            options.EnvActor);
        builder.Services.AddSingleton(tomeResolver);

        var app = builder.Build();
        app.UseStructuredErrors();
        app.MapMemoryEndpoints(
            memories,
            new MemoryEditor(store, embeddings, clock),
            new CorrectionService(store, clock, engineOptions),
            new MemoryQueryRunner(store, embeddings),
            store,
            tomeResolver,
            new ProjectTagResolver(services.GetService<IProjectRegistry>() ?? new ProjectRegistry([])),
            services.GetService<ICallerPathProvider>() ?? new ApiCallerPath());
        app.MapScryEndpoints(new ScryQueryRunner(memories, store, tomeResolver));
        app.MapPersonaEndpoints(persona, tomeResolver);
        app.MapSettingsEndpoints(engineOptions, options);
        app.MapStatsEndpoints(memories, tomeResolver);
        app.MapTomesEndpoints(memories, tomeResolver);
        app.MapHealthEndpoints(store, embeddings, clock);
        app.MapVersionEndpoints();
        app.MapAgentEndpoints(new AgentActivityProjection(store, clock));
        app.UseSpaFrontend(options);

        return app;
    }
}
