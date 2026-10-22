using System.Text;
using Grimoire.Infrastructure.Autostart;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Autostart;

public sealed class AutostartEntryWriterTests : IDisposable
{
    private const string Binary = "/opt/grimoire/bin/grimoire";

    private static readonly DateTime PinnedTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "grimoire-autostart-tests", Guid.NewGuid().ToString("N"));

    private readonly string _entryPath;

    public AutostartEntryWriterTests()
    {
        _entryPath = Path.Combine(_directory, AutostartEntryWriter.FileName);
    }

    [Fact]
    public async Task Enable_FreshDirectory_WritesTheNamedBinaryAsTheLoginCommand()
    {
        var writer = new AutostartEntryWriter(_entryPath, Binary);

        var change = await writer.EnableAsync(CancellationToken.None);

        change.ShouldBe(AutostartEntryChange.Written);
        var lines = (await File.ReadAllTextAsync(_entryPath)).Split('\n');
        lines.ShouldContain("Type=Application");
        lines.ShouldContain("Name=Grimoire");
        lines.ShouldContain("X-GNOME-Autostart-enabled=true");
        lines.ShouldContain($"Exec={Binary} ui");
    }

    [Fact]
    public async Task Enable_WhenTheEntryAlreadyMatches_ReportsUpToDateAndNeverRewritesIt()
    {
        var writer = new AutostartEntryWriter(_entryPath, Binary);
        await writer.EnableAsync(CancellationToken.None);
        var written = await File.ReadAllBytesAsync(_entryPath);
        File.SetLastWriteTimeUtc(_entryPath, PinnedTime);

        var change = await writer.EnableAsync(CancellationToken.None);

        change.ShouldBe(AutostartEntryChange.UpToDate);
        File.GetLastWriteTimeUtc(_entryPath).ShouldBe(PinnedTime);
        (await File.ReadAllBytesAsync(_entryPath)).ShouldBe(written);
    }

    [Fact]
    public async Task Enable_WhenTheEntryNamesAnotherBinary_RewritesIt()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_entryPath, "[Desktop Entry]\nExec=/old/grimoire ui\n");

        var change = await new AutostartEntryWriter(_entryPath, Binary).EnableAsync(CancellationToken.None);

        change.ShouldBe(AutostartEntryChange.Written);
        (await File.ReadAllTextAsync(_entryPath)).ShouldContain($"Exec={Binary} ui");
    }

    [Fact]
    public async Task Enable_LeavesNoTempRemainsBesideTheEntry()
    {
        await new AutostartEntryWriter(_entryPath, Binary).EnableAsync(CancellationToken.None);

        Directory.EnumerateFiles(_directory).Select(file => Path.GetFileName(file)!).ShouldBe([AutostartEntryWriter.FileName]);
    }

    [Fact]
    public async Task Enable_WritesPlainLfBytesWithoutABom()
    {
        await new AutostartEntryWriter(_entryPath, Binary).EnableAsync(CancellationToken.None);

        var bytes = await File.ReadAllBytesAsync(_entryPath);
        bytes.Take(3).ShouldNotBe(Encoding.UTF8.GetPreamble());
        bytes.ShouldNotContain((byte)'\r');
        bytes[^1].ShouldBe((byte)'\n');
    }

    [Fact]
    public async Task Enable_RelativeBinaryPath_WritesTheResolvedAbsoluteTarget()
    {
        await new AutostartEntryWriter(_entryPath, "bin/grimoire").EnableAsync(CancellationToken.None);

        var resolved = Path.GetFullPath("bin/grimoire");
        resolved.ShouldStartWith(Path.DirectorySeparatorChar.ToString());
        (await File.ReadAllTextAsync(_entryPath)).ShouldContain($"Exec={resolved} ui");
    }

    [Fact]
    public async Task Enable_BinaryPathWithASpace_QuotesTheExecTarget()
    {
        var binary = "/opt/a dir/grimoire";

        await new AutostartEntryWriter(_entryPath, binary).EnableAsync(CancellationToken.None);

        (await File.ReadAllTextAsync(_entryPath)).ShouldContain($"Exec=\"{binary}\" ui");
    }

    [Fact]
    public async Task Disable_WhenTheEntryIsThere_RemovesIt()
    {
        var writer = new AutostartEntryWriter(_entryPath, Binary);
        await writer.EnableAsync(CancellationToken.None);

        var change = await writer.DisableAsync(CancellationToken.None);

        change.ShouldBe(AutostartEntryChange.Removed);
        File.Exists(_entryPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Disable_WhenNothingWasWritten_ReportsAbsentAndWritesNothing()
    {
        var change = await new AutostartEntryWriter(_entryPath, Binary).DisableAsync(CancellationToken.None);

        change.ShouldBe(AutostartEntryChange.Absent);
        Directory.Exists(_directory).ShouldBeFalse();
    }

    [Fact]
    public async Task Enable_WithoutAnInjectedBinary_NamesTheRunningBinaryOfThisProcess()
    {
        var running = Path.GetFullPath(Environment.ProcessPath!);
        running.ShouldNotBeNullOrEmpty();

        await new AutostartEntryWriter(_entryPath).EnableAsync(CancellationToken.None);

        var entry = await File.ReadAllTextAsync(_entryPath);
        entry.ShouldContain(running);
        entry.ShouldContain(" ui");
    }

    [Fact]
    public void DefaultEntryPath_SitsUnderTheUserAutostartDirectoryAndIsOnlyInspectedHere()
    {
        var separator = Path.DirectorySeparatorChar;

        AutostartEntryWriter.DefaultEntryPath
            .ShouldEndWith($".config{separator}autostart{separator}{AutostartEntryWriter.FileName}");
        Path.IsPathRooted(AutostartEntryWriter.DefaultEntryPath).ShouldBeTrue();
        new AutostartEntryWriter().EntryPath.ShouldBe(AutostartEntryWriter.DefaultEntryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
