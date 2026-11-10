using System.Text;
using Grimoire.Infrastructure.Configuration;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Configuration;

public sealed class InstallBlockFileWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "grimoire-infra-tests", Guid.NewGuid().ToString("N"));

    public InstallBlockFileWriterTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Upsert_MissingFile_SkipsWithoutCreating()
    {
        var path = PathOf("claude.md");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Skipped);

        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void Upsert_ExistingContent_AppendsBlockAndKeepsBackup()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, "intro\n");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Updated);

        File.ReadAllText(path).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
        File.ReadAllText(Directory.GetFiles(_directory, "*.bak").ShouldHaveSingleItem()).ShouldBe("intro\n");
        Directory.GetFiles(_directory, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public void Upsert_UnchangedContent_SkipsTheRewrite()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, $"intro\n\n{InstallBlock.Text}\n");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Unchanged);

        Directory.GetFiles(_directory, "*.bak").ShouldBeEmpty();
    }

    [Fact]
    public void Upsert_StaleBlock_ReplacesInPlace()
    {
        var path = PathOf("claude.md");
        var stale = InstallBlock.Text.Replace("## Grimoire — agent memory", "## Stale heading", StringComparison.Ordinal);
        File.WriteAllText(path, $"head\n\n{stale}\n\nfoot\n");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Updated);

        File.ReadAllText(path).ShouldBe($"head\n\n{InstallBlock.Text}\n\nfoot\n");
    }

    [Fact]
    public void Upsert_EmptyFile_FillsItWithTheBlock()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, string.Empty);

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Created);

        File.ReadAllText(path).ShouldBe(InstallBlock.Text + "\n");
    }

    [Fact]
    public void Upsert_PreservesTheUtf8Bom()
    {
        var path = PathOf("claude.md");
        File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF]);
        File.AppendAllText(path, "intro\n");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Updated);

        var bytes = File.ReadAllBytes(path);
        bytes[0].ShouldBe((byte)0xEF);
        bytes[1].ShouldBe((byte)0xBB);
        bytes[2].ShouldBe((byte)0xBF);
        Encoding.UTF8.GetString(bytes[3..]).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
    }

    [Fact]
    public void Upsert_LeavesPlainFilesWithoutBom()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, "intro\n");

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Updated);

        File.ReadAllBytes(path)[0].ShouldNotBe((byte)0xEF);
    }

    [Fact]
    public void Upsert_PreservesTheUtf16LeBom()
    {
        var path = PathOf("claude.md");
        File.WriteAllBytes(path, Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("intro\n")).ToArray());

        InstallBlockFileWriter.Upsert(path, InstallBlock.Text).ShouldBe(InstallBlockOutcome.Updated);

        var bytes = File.ReadAllBytes(path);
        bytes[0].ShouldBe((byte)0xFF);
        bytes[1].ShouldBe((byte)0xFE);
        Encoding.Unicode.GetString(bytes[2..]).ShouldBe($"intro\n\n{InstallBlock.Text}\n");
    }

    [Fact]
    public void Remove_StripsTheBlockAndKeepsABackup()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, $"intro\n\n{InstallBlock.Text}\n");

        InstallBlockFileWriter.Remove(path).ShouldBe(InstallBlockOutcome.Removed);

        File.ReadAllText(path).ShouldBe("intro\n");
        Directory.GetFiles(_directory, "*.bak").ShouldNotBeEmpty();
    }

    [Fact]
    public void Remove_FileWithoutBlock_IsUnchanged()
    {
        var path = PathOf("claude.md");
        File.WriteAllText(path, "plain\n");

        InstallBlockFileWriter.Remove(path).ShouldBe(InstallBlockOutcome.Unchanged);

        File.ReadAllText(path).ShouldBe("plain\n");
    }

    [Fact]
    public void Remove_MissingFile_SkipsWithoutCreating()
    {
        var path = PathOf("claude.md");

        InstallBlockFileWriter.Remove(path).ShouldBe(InstallBlockOutcome.Skipped);

        File.Exists(path).ShouldBeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string PathOf(string name) => Path.Combine(_directory, name);
}
