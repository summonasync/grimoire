using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Embeddings;
using Grimoire.Infrastructure.Memory;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class CliServicesTests
{
    [Fact]
    public void Build_ResolvesAdaptersAgainstConfiguredSettings()
    {
        using var provider = CliServices.Build(Options());

        provider.GetRequiredService<IMemoryStore>().ShouldBeOfType<QdrantMemoryStore>();
        provider.GetRequiredService<IEmbeddingProvider>().ShouldBeOfType<OllamaEmbeddingProvider>();
        provider.GetRequiredService<IClock>().ShouldBeOfType<SystemClock>();
        provider.GetRequiredService<QdrantCollectionBootstrapper>().ShouldNotBeNull();
        provider.GetRequiredService<IActorClaimProvider>().ShouldBeOfType<ActorClaimProvider>();
    }

    [Fact]
    public void Build_BindsEngineOptionsFromConfigNotDefaults()
    {
        using var provider = CliServices.Build(Options());

        var options = provider.GetRequiredService<MemoryEngineOptions>();
        options.DefaultTome.ShouldBe("platform");
        options.SessionTtlHours.ShouldBe(5);
        options.DedupSimilarityThreshold.ShouldBe(0.75);
        options.TombstoneRetentionDays.ShouldBe(9);
        options.RecencyHalfLives.ProjectDays.ShouldBe(21);
        options.RecencyHalfLives.SessionDays.ShouldBe(0.5);
    }

    [Fact]
    public void Build_RegistersFacesAsSingletons()
    {
        using var provider = CliServices.Build(Options());

        provider.GetRequiredService<MemoryService>().ShouldBeSameAs(provider.GetRequiredService<MemoryService>());
        provider.GetRequiredService<PersonaService>().ShouldBeSameAs(provider.GetRequiredService<PersonaService>());
    }

    private static GrimoireOptions Options() => new()
    {
        DefaultTome = "platform",
        Memory = new MemoryOptions
        {
            SessionTtlHours = 5,
            DedupSimilarityThreshold = 0.75,
            TombstoneRetentionDays = 9,
            RecencyHalfLifeDays = new RecencyHalfLifeOptions { Project = 21 },
        },
    };
}
