using Grimoire.Core;

namespace Grimoire.Api.Querying;

public static class FieldsParam
{
    public static IReadOnlyList<string>? Resolve(string? raw, ReadProjectionSurface surface)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var fields = raw.Split(',').Select(field => field.Trim()).ToArray();
        var allowed = string.Join(", ", surface.AllowedKeys);
        if (fields.Any(string.IsNullOrEmpty))
        {
            throw new MemoryQueryException(
                "invalid_fields",
                $"fields must be a comma-separated list of field names; empty entries are not allowed. Allowed fields: {allowed}.");
        }

        try
        {
            return ReadProjection.Resolve(fields, compact: false, surface);
        }
        catch (ReadProjectionValidationException ex)
        {
            throw new MemoryQueryException("invalid_fields", ex.Message);
        }
    }
}
