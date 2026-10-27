using Grimoire.Core;

namespace Grimoire.Mcp;

internal static class DateParams
{
    public static (CreatedAtRange Range, ToolError? Error) Parse(string? since, string? until)
    {
        try
        {
            return (CreatedAtRange.Parse(since, until), null);
        }
        catch (ArgumentException failure)
        {
            return (default, new ToolError(ToolErrorCodes.InvalidRequest, failure.Message, failure.ParamName));
        }
    }
}
