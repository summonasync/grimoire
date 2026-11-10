using System.Security.Cryptography;
using System.Text;

namespace Grimoire.Infrastructure.Tomes;

public static class TomeManifestKeyId
{
    private const string NamespaceId = "b3ff7d51-8c0e-4f2a-9d47-1e6a5c2b8f30";

    public static Guid For(string key)
    {
        var raw = Guid.Parse(NamespaceId).ToByteArray();
        byte[] namespaceBytes =
        [
            raw[3], raw[2], raw[1], raw[0],
            raw[5], raw[4],
            raw[7], raw[6],
            raw[8], raw[9], raw[10], raw[11], raw[12], raw[13], raw[14], raw[15],
        ];

        var hash = SHA1.HashData([.. namespaceBytes, .. Encoding.ASCII.GetBytes(key)]);
        var bytes = hash.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(
        [
            bytes[3], bytes[2], bytes[1], bytes[0],
            bytes[5], bytes[4],
            bytes[7], bytes[6],
            bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15],
        ]);
    }
}
