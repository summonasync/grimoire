using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class TomeManifestServiceTests
{
    private static readonly MemoryActor Actor = MemoryActor.Agent("dotnet-coder", "claude");

    private readonly FakeTomeManifestStore _store = new();

    private readonly FakeClock _clock = new();

    private readonly TomeManifestService _service;

    public TomeManifestServiceTests()
    {
        _service = new TomeManifestService(_store, _clock);
    }

    [Fact]
    public async Task Create_OnFreshKey_ReturnsCreatedWithNormalizedFieldsAndProvenance()
    {
        var result = await _service.CreateAsync(" Dev-Env ", "  Project work and lessons  ", Actor, CancellationToken.None);

        result.Outcome.ShouldBe(TomeManifestOutcome.Created);
        var manifest = result.Manifest.ShouldNotBeNull();
        manifest.Key.ShouldBe("dev-env");
        manifest.Description.ShouldBe("Project work and lessons");
        manifest.CreatedBy.ShouldBe("dotnet-coder+claude");
        manifest.CreatedAt.ShouldBe(FakeClock.DefaultStart);
        manifest.UpdatedAt.ShouldBe(FakeClock.DefaultStart);
        _store.Manifests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Create_OnExistingManifest_ReturnsAlreadyExistsWithoutRewriting()
    {
        await _service.CreateAsync("dev", "Project work", Actor, CancellationToken.None);
        var before = _store.Manifests.Single();

        var result = await _service.CreateAsync("dev", "Different description", Actor, CancellationToken.None);

        result.Outcome.ShouldBe(TomeManifestOutcome.AlreadyExists);
        result.Manifest.ShouldBeNull();
        _store.Manifests.Single().ShouldBe(before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithBlankDescription_ThrowsValidation(string description)
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync("dev", description, Actor, CancellationToken.None));

        exception.ParamName.ShouldBe("description");
        _store.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithDescriptionOver280_ThrowsValidation()
    {
        var description = new string('x', TomeManifestService.MaxDescriptionLength + 1);

        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync("dev", description, Actor, CancellationToken.None));

        exception.ParamName.ShouldBe("description");
        _store.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WithDescriptionAt280_IsAccepted()
    {
        var description = new string('x', TomeManifestService.MaxDescriptionLength);

        var result = await _service.CreateAsync("dev", description, Actor, CancellationToken.None);

        result.Outcome.ShouldBe(TomeManifestOutcome.Created);
        result.Manifest!.Description.Length.ShouldBe(TomeManifestService.MaxDescriptionLength);
    }

    [Theory]
    [InlineData("Not A Tome!")]
    [InlineData("  ")]
    public async Task Create_WithInvalidKey_ThrowsValidation(string key)
    {
        var exception = await Should.ThrowAsync<ArgumentException>(
            () => _service.CreateAsync(key, "Project work", Actor, CancellationToken.None));

        exception.ParamName.ShouldBe("key");
        _store.Manifests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_OnExistingManifest_ReturnsUpdatedWithNewDescriptionAndTimestamp()
    {
        await _service.CreateAsync("dev", "Project work", Actor, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));

        var result = await _service.UpdateAsync("dev", "Domain work", Actor, CancellationToken.None);

        result.Outcome.ShouldBe(TomeManifestOutcome.Updated);
        result.Manifest.ShouldNotBeNull().Description.ShouldBe("Domain work");
        result.Manifest.UpdatedAt.ShouldBe(_clock.UtcNow);
        var stored = _store.Manifests.Single();
        stored.Description.ShouldBe("Domain work");
        stored.UpdatedAt.ShouldBe(_clock.UtcNow);
        stored.CreatedAt.ShouldBe(FakeClock.DefaultStart);
        stored.CreatedBy.ShouldBe("dotnet-coder+claude");
    }

    [Fact]
    public async Task Update_OnMissingManifest_ReturnsNotFound()
    {
        var result = await _service.UpdateAsync("ghost", "Description", Actor, CancellationToken.None);

        result.Outcome.ShouldBe(TomeManifestOutcome.NotFound);
        result.Manifest.ShouldBeNull();
    }

    [Fact]
    public async Task Update_WithBlankDescription_ThrowsValidation()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _service.UpdateAsync("dev", "  ", Actor, CancellationToken.None));
    }
}
