using Grimoire.Core;

namespace Grimoire.Mcp;

internal static class SessionIdentityParams
{
    public const int MaxAliasLength = 32;

    public const int MaxVendorLength = 32;

    public static (MemoryActor Identity, ToolError? Error) Parse(
        string? alias,
        string? vendor,
        string aliasParam,
        string vendorParam)
    {
        var trimmedAlias = alias?.Trim();
        if (string.IsNullOrEmpty(trimmedAlias))
        {
            return (default, new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"{aliasParam} is required: pass 1-{MaxAliasLength} characters without whitespace inside.",
                aliasParam));
        }

        if (trimmedAlias.Length > MaxAliasLength)
        {
            return (default, new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"{aliasParam}: '{trimmedAlias}' is {trimmedAlias.Length} characters; the limit is {MaxAliasLength}.",
                aliasParam));
        }

        if (trimmedAlias.Any(char.IsWhiteSpace))
        {
            return (default, new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"{aliasParam}: '{trimmedAlias}' must not contain whitespace; pass a single token of " +
                $"1-{MaxAliasLength} characters.",
                aliasParam));
        }

        var trimmedVendor = vendor?.Trim();
        if (trimmedVendor is { Length: > MaxVendorLength })
        {
            return (default, new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"{vendorParam}: '{trimmedVendor}' is {trimmedVendor.Length} characters; the limit is {MaxVendorLength}.",
                vendorParam));
        }

        return (new MemoryActor(trimmedAlias, string.IsNullOrEmpty(trimmedVendor) ? null : trimmedVendor), null);
    }
}
