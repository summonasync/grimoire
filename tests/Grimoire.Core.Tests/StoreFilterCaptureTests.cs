using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class StoreFilterCaptureTests
{
    private const string Content = "note";

    private static readonly IReadOnlyDictionary<string, float[]> Vectors = new Dictionary<string, float[]>
    {
        [Content] = [1f, 0f],
    };

    private readonly FakeMemoryStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly MemoryService _service;

    public StoreFilterCaptureTests()
    {
        _service = TestMemories.Service(_store, new FakeEmbeddingProvider(Vectors), _clock);
        Seed("crew");
    }

    [Fact]
    public async Task List_SeesTheFilterTheServicePassed()
    {
        var captured = new List<MemoryFilter>();
        _store.OnListAsync = captured.Add;

        await _service.ListAsync(new MemoryFilter { Tome = "crew", Statuses = MemoryStatuses.All }, CancellationToken.None);

        captured.ShouldHaveSingleItem();
        captured[0].Tome.ShouldBe("crew");
        captured[0].Statuses.ShouldBe(MemoryStatuses.All);
    }

    [Fact]
    public async Task Count_SeesTheFilterTheServicePassed()
    {
        var captured = new List<MemoryFilter>();
        _store.OnCountAsync = captured.Add;

        await _service.RetomeAsync(new MemoryFilter { Tome = "crew" }, "archive", CancellationToken.None);

        captured.ShouldNotBeEmpty();
        captured.ShouldContain(filter => filter.Tome == "crew");
    }

    [Fact]
    public async Task Count_HookCapturesTheFilterHandedToTheFake()
    {
        MemoryFilter? captured = null;
        _store.OnCountAsync = filter => captured = filter;

        await _store.CountAsync(new MemoryFilter { Tome = "crew", Statuses = MemoryStatuses.All }, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.Statuses.ShouldBe(MemoryStatuses.All);
        captured.Status.ShouldBeNull();
    }

    private void Seed(string tome) =>
        _store.Seed(TestMemories.Create(Content, [1f, 0f], created: FakeClock.DefaultStart, tome: tome), [1f, 0f]);
}
