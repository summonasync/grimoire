namespace Grimoire.Api.Tomes;

internal static class TomeParam
{
    public static string InvalidMessage(string paramName, string? candidate) =>
        candidate is null
            ? $"{paramName} is required and must be a lowercase kebab-case tome key."
            : $"{paramName} must be a lowercase kebab-case tome key; '{candidate}' is not a valid tome key.";
}