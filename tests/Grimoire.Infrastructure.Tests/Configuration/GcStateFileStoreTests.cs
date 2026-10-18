using Grimoire.Core;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class GcStateFileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "grimoire-gc-state-tests", Guid.NewGuid().ToString("N"));

    private readonly string _path;

    private readonly GcStateFileStore _store;

    public GcStateFileStoreTests()
    {
        _path = Path.Combine(_directory, "gc-state.json");
        _store = new GcStateFileStore(_path);
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsTheHeartbeat()
    {
        var written = new GcState
        {
            LastAttemptAt = new DateTimeOffset(2026, 10, 19, 11, 0, 0, TimeSpan.Zero),
            LastSuccessAt = new DateTimeOffset(2026, 10, 19, 10, 59, 0, TimeSpan.Zero),
            RemovedExpired = 3,
            RemovedTombstones = 1,
            LastError = null,
        };

        await _store.WriteAsync(written, CancellationToken.None);
        var read = await _store.ReadAsync(CancellationToken.None);

        read.IsUnknown.ShouldBeFalse();
        read.LastAttemptAt.ShouldBe(written.LastAttemptAt);
        read.LastSuccessAt.ShouldBe(written.LastSuccessAt);
        read.RemovedExpired.ShouldBe(3);
        read.RemovedTombstones.ShouldBe(1);
        read.LastError.ShouldBeNull();
    }

    [Fact]
    public async Task Write_ReplacesTheFileAndLeavesNoTempRemains()
    {
        await _store.WriteAsync(new GcState { LastAttemptAt = FakeNow(), LastSuccessAt = FakeNow(), RemovedExpired = 1 }, CancellationToken.None);
        await _store.WriteAsync(new GcState { LastAttemptAt = FakeNow(), LastSuccessAt = FakeNow(), RemovedExpired = 7, RemovedTombstones = 2 }, CancellationToken.None);

        var read = await _store.ReadAsync(CancellationToken.None);
        Directory.EnumerateFiles(_directory).Select(file => Path.GetFileName(file)!).ShouldBe(["gc-state.json"]);
        read.RemovedExpired.ShouldBe(7);
    }

    [Fact]
    public async Task Write_CreatesTheGrimoireDirectoryWhenMissing()
    {
        Directory.Exists(_directory).ShouldBeFalse();

        await _store.WriteAsync(new GcState { LastAttemptAt = FakeNow() }, CancellationToken.None);

        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_DoesNotPersistDerivedOrUnknownFields()
    {
        await _store.WriteAsync(new GcState { LastAttemptAt = FakeNow(), Stale = true, UnknownReason = "should never land on disk" }, CancellationToken.None);

        var text = await File.ReadAllTextAsync(_path);
        text.ShouldNotContain("stale");
        text.ShouldNotContain("unknownReason");
        text.ShouldContain("lastAttemptAt");
    }

    [Fact]
    public async Task Read_MissingFile_ReportsUnknownWithReason()
    {
        var state = await _store.ReadAsync(CancellationToken.None);

        state.IsUnknown.ShouldBeTrue();
        state.UnknownReason.ShouldBe("gc state file does not exist yet");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"lastAttemptAt\": \"not a date\"}")]
    public async Task Read_CorruptFile_ReportsUnknownWithDiagnosableReason(string content)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, content);

        var state = await _store.ReadAsync(CancellationToken.None);

        state.IsUnknown.ShouldBeTrue();
        state.UnknownReason.ShouldNotBeNull();
        state.UnknownReason.ShouldContain("gc state file");
    }

    [Fact]
    public async Task Read_EmptyObject_ReportsNoRecordedAttempts()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, "{}");

        var state = await _store.ReadAsync(CancellationToken.None);

        state.IsUnknown.ShouldBeTrue();
        state.UnknownReason.ShouldBe("gc state file holds no recorded sweep attempts");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private static DateTimeOffset FakeNow() => new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
}
