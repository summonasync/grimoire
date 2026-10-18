using Grimoire.Core;

namespace Grimoire.Mcp;

internal static class TomeParams
{
    public static ToolError? Validate(string? tome, string paramName)
    {
        if (tome is null || TomeKey.TryNormalize(tome, out _))
        {
            return null;
        }

        return new ToolError(
            ToolErrorCodes.InvalidRequest,
            $"{paramName} must be a lowercase kebab-case tome key; '{tome}' is not a valid tome key.",
            paramName);
    }

    public static ToolError? Validate(IReadOnlyList<string> tomes, string paramName)
    {
        foreach (var tome in tomes)
        {
            if (TomeKey.TryNormalize(tome, out _))
            {
                continue;
            }

            return new ToolError(
                ToolErrorCodes.InvalidRequest,
                $"Each entry of {paramName} must be a lowercase kebab-case tome key; '{tome}' is not a valid tome key.",
                paramName);
        }

        return null;
    }
}
