using System.Globalization;
using System.Text;

namespace Grimoire.Infrastructure.Configuration;

public static class InstallBlockFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    private static readonly UnicodeEncoding Utf16LeWithBom = new(bigEndian: false, byteOrderMark: true);

    private static readonly UnicodeEncoding Utf16BeWithBom = new(bigEndian: true, byteOrderMark: true);

    public static InstallBlockOutcome Upsert(string path, string block)
    {
        if (!File.Exists(path))
        {
            return InstallBlockOutcome.Skipped;
        }

        var (current, encoding) = ReadText(path);
        var merged = InstallBlockMerger.Merge(current, block);
        if (merged == current)
        {
            return InstallBlockOutcome.Unchanged;
        }

        WriteChanged(path, merged, encoding);
        return current.Length == 0 ? InstallBlockOutcome.Created : InstallBlockOutcome.Updated;
    }

    public static InstallBlockOutcome Remove(string path)
    {
        if (!File.Exists(path))
        {
            return InstallBlockOutcome.Skipped;
        }

        var (current, encoding) = ReadText(path);
        var stripped = InstallBlockMerger.Strip(current);
        if (stripped == current)
        {
            return InstallBlockOutcome.Unchanged;
        }

        WriteChanged(path, stripped, encoding);
        return InstallBlockOutcome.Removed;
    }

    private static (string Text, Encoding Encoding) ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Utf8WithBom);
        }

        if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Utf16LeWithBom);
        }

        if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Utf16BeWithBom);
        }

        return (Encoding.UTF8.GetString(bytes), Utf8NoBom);
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteChanged(string path, string text, Encoding encoding)
    {
        EnsureBackup(path);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(tempPath, encoding.GetBytes(text));
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void EnsureBackup(string path)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var backupPath = $"{path}.{stamp}.bak";
        var collision = 0;
        while (File.Exists(backupPath))
        {
            collision++;
            backupPath = $"{path}.{stamp}-{collision}.bak";
        }

        File.Copy(path, backupPath);
    }
}
