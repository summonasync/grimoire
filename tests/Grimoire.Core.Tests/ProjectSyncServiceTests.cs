using Grimoire.Core;
using Grimoire.Core.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Core.Tests;

public sealed class ProjectSyncServiceTests
{
    [Fact]
    public async Task Sync_NewBoardProject_AddsWithLocallyGeneratedAliasAndBoardPath()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "grimoire", Paths = ["/src/grimoire"] },
        ]);
        var service = MakeService(store, Board("5366", "Slurp.AI", "index it", "/mnt/Slurp.AI"));

        var report = await service.SyncAsync(CancellationToken.None);

        var added = store.Records.Single(record => record.BoardProjectId == "5366");
        added.Alias.ShouldBe("slurp-ai");
        added.Paths.ShouldBe(["/mnt/Slurp.AI"]);
        added.DisplayName.ShouldBe("Slurp.AI");
        added.Description.ShouldBe("index it");
        added.BoardGone.ShouldBeFalse();
        report.Added.ShouldHaveSingleItem().Subject.ShouldBe("slurp-ai");
        report.Added.Single().Detail.ShouldContain("/mnt/Slurp.AI");
    }

    [Fact]
    public async Task Sync_TakenAlias_GeneratesCollisionSuffix()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "slurp-ai", Paths = ["/src/other"] },
        ]);
        var service = MakeService(store, Board("b1", "Slurp.AI", null, "/mnt/Slurp.AI"));

        await service.SyncAsync(CancellationToken.None);

        store.Records.Single(record => record.BoardProjectId == "b1").Alias.ShouldBe("slurp-ai-2");
    }

    [Fact]
    public async Task Sync_MatchesByBoardIdAndRefreshesHintsOnly()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord
            {
                Alias = "grimoire",
                Paths = ["/src/grimoire", "/src/grimoire-wt"],
                BoardProjectId = "7450",
                DisplayName = "Old name",
                Description = "old",
            },
        ]);
        var service = MakeService(store, Board("7450", "Grimoire", "memory service", "/mnt/moved/elsewhere"));

        var report = await service.SyncAsync(CancellationToken.None);

        var record = store.Records.ShouldHaveSingleItem();
        record.Alias.ShouldBe("grimoire");
        record.Paths.ShouldBe(["/src/grimoire", "/src/grimoire-wt"]);
        record.DisplayName.ShouldBe("Grimoire");
        record.Description.ShouldBe("memory service");
        record.BoardProjectId.ShouldBe("7450");
        report.Updated.ShouldHaveSingleItem().Subject.ShouldBe("grimoire");
    }

    [Fact]
    public async Task Sync_UnchangedRecord_IsNotRewrittenAndNotReported()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1", DisplayName = "Alpha" },
        ]);
        var service = MakeService(store, Board("b1", "Alpha", null, "/src/alpha"));

        var report = await service.SyncAsync(CancellationToken.None);

        report.Updated.ShouldBeEmpty();
        report.Added.ShouldBeEmpty();
        report.Gone.ShouldBeEmpty();
        report.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_BoardProjectVanished_MarksRecordAndKeepsAlias()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1", DisplayName = "Alpha" },
            new ProjectAliasRecord { Alias = "manual", Paths = ["/src/manual"] },
        ]);
        var service = MakeService(store, []);

        var report = await service.SyncAsync(CancellationToken.None);

        var alpha = store.Records.Single(record => record.Alias == "alpha");
        alpha.BoardGone.ShouldBeTrue();
        alpha.BoardProjectId.ShouldBe("b1");
        alpha.Paths.ShouldBe(["/src/alpha"]);
        store.Records.Select(record => record.Alias).ShouldBe(["alpha", "manual"]);
        store.Records.Single(record => record.Alias == "manual").BoardGone.ShouldBeFalse();
        report.Gone.ShouldHaveSingleItem().Subject.ShouldBe("alpha");
        report.Gone.Single().Detail.ShouldContain("remove it explicitly");
    }

    [Fact]
    public async Task Sync_AlreadyGoneRecord_IsReportedButNotRewritten()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1", BoardGone = true },
        ]);
        var service = MakeService(store, []);

        var report = await service.SyncAsync(CancellationToken.None);

        report.Gone.ShouldHaveSingleItem().Subject.ShouldBe("alpha");
    }

    [Fact]
    public async Task Sync_ReappearingBoardProject_ClearsTheGoneMark()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1", BoardGone = true },
        ]);
        var service = MakeService(store, Board("b1", "Alpha", null, "/src/alpha"));

        var report = await service.SyncAsync(CancellationToken.None);

        store.Records.ShouldHaveSingleItem().BoardGone.ShouldBeFalse();
        report.Updated.ShouldHaveSingleItem().Detail.ShouldContain("board-gone flag cleared");
        report.Gone.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_BoardProjectWithoutDiskPath_IsSkippedNotAdded()
    {
        var store = new FakeProjectRegistryStore();
        var service = MakeService(store, Board("b1", "Headless", null, null));

        var report = await service.SyncAsync(CancellationToken.None);

        store.Records.ShouldBeEmpty();
        report.Added.ShouldBeEmpty();
        report.Skipped.ShouldHaveSingleItem().Subject.ShouldBe("Headless");
        report.Skipped.Single().Detail.ShouldContain("no disk path");
    }

    [Fact]
    public async Task Sync_BoardProjectWhosePathIsAlreadyRegistered_IsSkipped()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "resident", Paths = ["/src/occupied"] },
        ]);
        var service = MakeService(store, Board("b1", "Intruder", null, "/src/occupied"));

        var report = await service.SyncAsync(CancellationToken.None);

        store.Records.ShouldHaveSingleItem().Alias.ShouldBe("resident");
        report.Skipped.ShouldHaveSingleItem().Detail.ShouldContain("already registered locally");
    }

    [Fact]
    public async Task Sync_HostDown_RefusesWithoutTouchingTheStore()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1" },
        ]);
        var source = new FakeBoardProjectSource { Failure = new UpstreamUnavailableException("overmind") };
        var service = new ProjectSyncService(source, store);

        await Should.ThrowAsync<UpstreamUnavailableException>(() => service.SyncAsync(CancellationToken.None));

        store.Records.ShouldHaveSingleItem().BoardGone.ShouldBeFalse();
    }

    [Fact]
    public async Task Sync_RegistryChangedUnderneath_RefusesAndWritesNothing()
    {
        var store = new FakeProjectRegistryStore(
        [
            new ProjectAliasRecord { Alias = "alpha", Paths = ["/src/alpha"], BoardProjectId = "b1", DisplayName = "Alpha" },
        ]);
        store.Interceptor = records => records.Add(
            new ProjectAliasRecord { Alias = "sneaky", Paths = ["/src/sneaky"] });
        var service = MakeService(store, Board("b1", "Renamed", null, "/src/alpha"));

        await Should.ThrowAsync<ProjectRegistryStaleException>(() => service.SyncAsync(CancellationToken.None));

        store.Records.Select(record => record.Alias).ShouldBe(["alpha", "sneaky"]);
        store.Records.Single(record => record.Alias == "alpha").DisplayName.ShouldBe("Alpha");
    }

    private static ProjectSyncService MakeService(FakeProjectRegistryStore store, params BoardProject[] board) =>
        new(new FakeBoardProjectSource(board), store);

    private static BoardProject Board(string id, string name, string? description, string? diskPath) =>
        new(id, name, description, diskPath);
}
