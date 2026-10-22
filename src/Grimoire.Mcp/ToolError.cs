namespace Grimoire.Mcp;

internal sealed record ToolError(string Code, string Message, string? Param = null)
{
    public ToolError NothingApplied() => this with { Message = Message + " Nothing was applied by this call." };
}
