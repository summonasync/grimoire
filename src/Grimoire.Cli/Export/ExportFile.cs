using System.IO.Compression;

namespace Grimoire.Cli.Export;

internal static class ExportFile
{
    private static readonly byte[] GzipMagic = [0x1f, 0x8b];

    public static Stream OpenRead(string path)
    {
        var raw = File.OpenRead(path);
        Span<byte> magic = stackalloc byte[GzipMagic.Length];
        var read = raw.Read(magic);
        raw.Position = 0;

        return read == GzipMagic.Length && magic.SequenceEqual(GzipMagic)
            ? new GZipStream(raw, CompressionMode.Decompress)
            : raw;
    }

    public static Stream Create(string path, bool gzip) =>
        gzip
            ? new GZipStream(File.Create(path), CompressionLevel.Fastest)
            : File.Create(path);
}
