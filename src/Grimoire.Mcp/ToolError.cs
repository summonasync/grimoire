namespace Grimoire.Mcp;

internal sealed record ToolError(string Code, string Message, string? Param = null);
