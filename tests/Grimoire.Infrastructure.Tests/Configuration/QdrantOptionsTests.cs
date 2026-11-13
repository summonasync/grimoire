using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class QdrantOptionsTests
{
    [Fact]
    public void ManifestCollection_WhenUnset_DerivesFromTheMemoryCollection()
    {
        var options = new QdrantOptions { Collection = "grimoire" };

        options.ManifestCollection.ShouldBeNull();
        options.ManifestCollectionOrDefault.ShouldBe("grimoire-manifests");
    }

    [Fact]
    public void ManifestCollection_WhenSet_OverridesTheDerivedName()
    {
        var options = new QdrantOptions { Collection = "grimoire", ManifestCollection = "custom" };

        options.ManifestCollectionOrDefault.ShouldBe("custom");
    }

    [Fact]
    public void SessionCollection_WhenUnset_DerivesFromTheMemoryCollection()
    {
        var options = new QdrantOptions { Collection = "grimoire" };

        options.SessionCollection.ShouldBeNull();
        options.SessionCollectionOrDefault.ShouldBe("grimoire-sessions");
    }

    [Fact]
    public void SessionCollection_WhenSet_OverridesTheDerivedName()
    {
        var options = new QdrantOptions { Collection = "grimoire", SessionCollection = "custom-sessions" };

        options.SessionCollectionOrDefault.ShouldBe("custom-sessions");
    }
}
