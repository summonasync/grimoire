using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class PersonaServiceTests
{
    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        ["senior .NET developer"] = [1f, 0f, 0f],
        ["principal .NET developer"] = [1f, 0.2f, 0f],
        ["dry humor"] = [0f, 1f, 0f],
        ["dark theme everywhere"] = [0f, 0f, 1f],
        ["no meetings before noon"] = [1f, 1f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly PersonaService _service;

    public PersonaServiceTests()
    {
        _service = new PersonaService(_store, new FakeEmbeddingProvider(Vectors), _clock);
    }

    [Fact]
    public async Task Get_EmptyStore_ReturnsEmptySections()
    {
        var document = await _service.GetAsync(CancellationToken.None);

        document.Identity.ShouldBeEmpty();
        document.Tone.ShouldBeEmpty();
        document.Preferences.ShouldBeEmpty();
        document.Rules.ShouldBeEmpty();
    }

    [Fact]
    public async Task Set_CreatesOneRecordPerSection()
    {
        var document = new PersonaDocument
        {
            Identity = "senior .NET developer",
            Tone = "dry humor",
            Preferences = "dark theme everywhere",
            Rules = "no meetings before noon",
        };

        var stored = await _service.SetAsync(document, MemoryActor.Human, CancellationToken.None);

        stored.Identity.ShouldBe(document.Identity);
        _store.Records.Count.ShouldBe(4);
        _store.Records.ShouldAllBe(record => record.Scope == MemoryScope.Personal);
        _store.Records.ShouldAllBe(record => record.Kind == MemoryKind.Fact);
        _store.Records.ShouldAllBe(record => record.Actor == MemoryActor.Human);
        _store.Records.ShouldAllBe(record => record.Tags.Contains(PersonaSectionExtensions.PersonaTag));
        _store.Records.ShouldAllBe(record => record.CreatedAt == _clock.UtcNow);
        _store.Records.Any(record => record.Tags.Contains(PersonaSection.Identity.SectionTag())).ShouldBeTrue();
        _store.Records.Any(record => record.Tags.Contains(PersonaSection.Rules.SectionTag())).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_RoundTripsPreviouslyStoredDocument()
    {
        var document = new PersonaDocument
        {
            Identity = "senior .NET developer",
            Tone = "dry humor",
            Preferences = "dark theme everywhere",
            Rules = "no meetings before noon",
        };
        await _service.SetAsync(document, MemoryActor.Human, CancellationToken.None);

        var loaded = await _service.GetAsync(CancellationToken.None);

        loaded.ShouldBe(document);
    }

    [Fact]
    public async Task Set_UpdatesChangedSectionInPlaceAndSkipsUnchanged()
    {
        var document = new PersonaDocument
        {
            Identity = "senior .NET developer",
            Tone = "dry humor",
            Preferences = "dark theme everywhere",
            Rules = "no meetings before noon",
        };
        await _service.SetAsync(document, MemoryActor.Human, CancellationToken.None);
        var identityBefore = _store.Records.Single(record => record.Tags.Contains(PersonaSection.Identity.SectionTag()));
        var embeddings = new FakeEmbeddingProvider(Vectors);
        var service = new PersonaService(_store, embeddings, _clock);
        _clock.Advance(TimeSpan.FromHours(2));

        await service.SetAsync(document with { Identity = "principal .NET developer" }, MemoryActor.Human, CancellationToken.None);

        _store.Records.Count.ShouldBe(4);
        var identity = _store.Records.Single(record => record.Tags.Contains(PersonaSection.Identity.SectionTag()));
        identity.Id.ShouldBe(identityBefore.Id);
        identity.Content.ShouldBe("principal .NET developer");
        identity.CreatedAt.ShouldBe(_clock.UtcNow);
        var tone = _store.Records.Single(record => record.Tags.Contains(PersonaSection.Tone.SectionTag()));
        tone.Content.ShouldBe("dry humor");
        tone.CreatedAt.ShouldBe(FakeClock.DefaultStart);
        embeddings.Requests.ShouldBe(["principal .NET developer"]);
    }

    [Fact]
    public async Task Get_IgnoresNonPersonaPersonalFacts()
    {
        _store.Seed(TestMemories.Create(
            "likes coffee",
            [1f, 1f, 1f],
            scope: MemoryScope.Personal,
            kind: MemoryKind.Fact,
            tags: ["taste"]), [1f, 1f, 1f]);

        var document = await _service.GetAsync(CancellationToken.None);

        document.Identity.ShouldBeEmpty();
    }

    [Fact]
    public async Task Set_EmbedsSectionContentForRecall()
    {
        var document = new PersonaDocument { Identity = "senior .NET developer" };
        var embeddings = new FakeEmbeddingProvider(Vectors);
        var service = new PersonaService(_store, embeddings, _clock);

        await service.SetAsync(document, MemoryActor.Human, CancellationToken.None);

        embeddings.Requests.ShouldBe(["senior .NET developer"]);
        var identity = _store.Records.Single(record => record.Tags.Contains(PersonaSection.Identity.SectionTag()));
        identity.Content.ShouldBe("senior .NET developer");
        _store.Records.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Set_SparseDocument_LeavesUnsetSectionsUnstored()
    {
        var document = new PersonaDocument
        {
            Identity = "senior .NET developer",
            Rules = "no meetings before noon",
        };
        var embeddings = new FakeEmbeddingProvider(Vectors);
        var service = new PersonaService(_store, embeddings, _clock);

        var stored = await service.SetAsync(document, MemoryActor.Human, CancellationToken.None);
        var loaded = await service.GetAsync(CancellationToken.None);

        stored.Tone.ShouldBeEmpty();
        stored.Preferences.ShouldBeEmpty();
        loaded.ShouldBe(document);
        embeddings.Requests.ShouldBe(["senior .NET developer", "no meetings before noon"]);
    }
}
