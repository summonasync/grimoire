using Grimoire.Core;

namespace Grimoire.Api.Tomes;

internal static class ManifestParam
{
    public static string? DescriptionError(string value)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return "description must be 1..280 characters after trim; the submitted description is empty.";
        }

        return trimmed.Length > TomeManifestService.MaxDescriptionLength
            ? $"description must be 1..280 characters after trim; the submitted description is {trimmed.Length} characters long."
            : null;
    }
}
