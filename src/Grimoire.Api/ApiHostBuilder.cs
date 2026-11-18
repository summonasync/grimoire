using System.Text.Json;
using System.Text.Json.Serialization;
using Grimoire.Api.Agents;
using Grimoire.Api.Editing;
using Grimoire.Api.Endpoints;
using Grimoire.Api.Querying;
using Grimoire.Api.Sessions;
using Grimoire.Api.Tomes;
using Grimoire.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Grimoire.Api;

public static class ApiHostBuilder
{
    private static readonly MemoryActor SweepActor = MemoryActor.Agent("grimoire", "consolidator");

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
        var consolidationOptions = services.GetService<ConsolidationOptions>() ?? new ConsolidationOptions();
        var consolidationState = services.GetService<IConsolidationStateStore>();
        var memories = services.GetService<MemoryService>()
            ?? new MemoryService(store, embeddings, clock, engineOptions, gcOptions, gcState,
                consolidationOptions: consolidationOptions, consolidationStateStore: consolidationState);
        var persona = services.GetService<PersonaService>() ?? new PersonaService(store, embeddings, clock);

        var builder = WebApplication.CreateBuilder();
        // serve writes two logging stacks into the same stdout: the Cli's Serilog console sink and
        // this host's. The stamp verdict reaches the operator through the Cli line, so the Api copy
        // stays a record for other providers and stops rendering a second time here.
        builder.Logging.AddFilter<ConsoleLoggerProvider>(nameof(SpaHosting), LogLevel.None);
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
        // The bind guard proves where the socket listens, not who the request claims to target:
        // a DNS-rebinding browser reaches the same loopback socket with a hostile Host header.
        // Port-less entries accept any port (audit-2026-11-15 W1).
        builder.Services.Configure<HostFilteringOptions>(filtering =>
            filtering.AllowedHosts = ["127.0.0.1", "localhost", "grimoire.localhost"]);
        builder.Services.AddHostedService<LoopbackGuardService>();
        builder.Services.AddHostedService(_ => new GcSweeper(
            memories.GcAsync,
            clock,
            TimeSpan.FromMinutes(options.GcIntervalMinutes),
            gcState));
        builder.Services.AddHostedService(_ => new ConsolidationSweeper(
            cancellationToken => memories.ConsolidateAsync(SweepActor, cancellationToken),
            clock,
            consolidationOptions.SweepInterval,
            consolidationState));
        // The registry is a serve-side surface: the MCP child writes rows and never prunes
        // them, so the retention sweep is registered here and nowhere else. A provider
        // without a registry (hand-built bind and parity hosts) gets neither routes nor sweep.
        if (services.GetService<ISessionRegistry>() is { } sessionRegistry)
        {
            builder.Services.AddHostedService(_ => new SessionRetentionSweeper(
                sessionRegistry.PruneAsync,
                clock,
                TimeSpan.FromHours(options.SessionsRetentionHours),
                TimeSpan.FromMinutes(options.GcIntervalMinutes)));
        }

        var tomeResolver = new ApiTomeResolver(
            new TomeResolver(engineOptions),
            services.GetService<IActorClaimProvider>(),
            options.EnvTome,
            options.EnvActor,
            options.IdentityFile);
        builder.Services.AddSingleton(tomeResolver);

        var app = builder.Build();
        app.UseHostFiltering();
        app.UseStructuredErrors();
        app.MapMemoryEndpoints(
            memories,
            new MemoryEditor(store, embeddings, clock),
            new CorrectionService(store, clock, engineOptions),
            new MemoryQueryRunner(store, embeddings, engineOptions),
            store,
            tomeResolver,
            new ProjectTagResolver(services.GetService<IProjectRegistry>() ?? new ProjectRegistry([])),
            services.GetService<ICallerPathProvider>() ?? new ApiCallerPath());
        app.MapScryEndpoints(new ScryQueryRunner(memories, store, tomeResolver));
        app.MapPersonaEndpoints(persona, tomeResolver);
        app.MapSettingsEndpoints(engineOptions, options);
        app.MapStatsEndpoints(memories, tomeResolver);
        var manifestStore = services.GetRequiredService<ITomeManifestStore>();
        app.MapTomesEndpoints(
            memories,
            store,
            tomeResolver,
            services.GetService<TomeManifestService>() ?? new TomeManifestService(manifestStore, clock),
            services.GetService<TomePurgeService>() ?? new TomePurgeService(store, manifestStore),
            clock);
        app.MapGraphEndpoints(store, engineOptions);
        app.MapHealthEndpoints(store, embeddings, clock);
        app.MapVersionEndpoints();
        app.MapAgentEndpoints(new AgentActivityProjection(store, clock));
        // Hand-built containers (parity and bind tests) resolve no registry; mapping a route
        // over an absent surface would answer an empty list for a store that is not wired at all.
        if (services.GetService<ISessionRegistry>() is { } sessions)
        {
            app.MapSessionsEndpoints(new SessionHandshakeProjection(
                sessions, clock, options.SessionsRetentionHours));
        }
        app.UseSpaFrontend(options);

        return app;
    }
}
