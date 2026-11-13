using System.Text;
using System.Text.Json;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class DescriptionBudgetContractTests
{
    // Raised 16000 -> 16500 on 2026-11-13 by the owner, after the guard fired on a merge rather than on
    // bloat: this tree measures 16075 while origin/main was measured green under the old cap, so it took
    // two real features together - 045's `daily` parameter and 052's memory_note additions - to cross it,
    // and neither one alone. The ceiling is a tripwire for saturation, not a budget the surface is
    // allowed to grow into silently; at saturation the honest move is a deliberate raise with the
    // arithmetic recorded, not deleting documentation from whichever feature noticed last. Headroom
    // against the measured 16075: 425 chars across all 16 tools.
    private const int TotalDescriptionCeiling = 16500;

    private const int StartupContractCeiling = 2500;

    [Fact]
    public async Task TotalComposedDescriptions_StayUnderTheBudget()
    {
        await using var session = await McpTestSession.StartAsync();

        var rows = await MeasureTools(session);

        var total = rows.Sum(row => row.TotalChars);
        total.ShouldBeLessThanOrEqualTo(TotalDescriptionCeiling,
            customMessage: BudgetTable(rows, total, TotalDescriptionCeiling));
    }

    [Fact]
    public async Task BriefStartupContract_StaysUnderItsCeiling()
    {
        await using var session = await McpTestSession.StartAsync();
        await session.Client.CallToolAsync("memory_remember", new Dictionary<string, object?>
        {
            ["content"] = "budget probe standing rule",
            ["scope"] = "Global",
            ["kind"] = "Fact",
        });

        var result = await session.Client.CallToolAsync("memory_brief", new Dictionary<string, object?>());

        result.ShouldSucceed();
        var hint = result.Payload().GetProperty("hint").GetString();
        hint.ShouldNotBeNull();
        hint.Length.ShouldBeLessThanOrEqualTo(StartupContractCeiling,
            customMessage: $"startup contract hint is {hint.Length} chars against the {StartupContractCeiling} ceiling");
    }

    private static async Task<List<ToolBudget>> MeasureTools(McpTestSession session)
    {
        var tools = await session.Client.ListToolMap();

        return [.. tools.Values
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .Select(tool => new ToolBudget(tool.Name, tool.Description?.Length ?? 0, ParameterDescriptionChars(tool)))];
    }

    private static int ParameterDescriptionChars(McpClientTool tool)
    {
        if (!tool.JsonSchema.TryGetProperty("properties", out var properties))
        {
            return 0;
        }

        var total = 0;
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Value.TryGetProperty("description", out var description)
                && description.ValueKind == JsonValueKind.String)
            {
                total += description.GetString()!.Length;
            }
        }

        return total;
    }

    private static string BudgetTable(List<ToolBudget> rows, int total, int ceiling)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            $"description budget breach: {total} composed chars against the {ceiling} ceiling " +
            $"(tool description consts + parameter description texts across {rows.Count} tools)");
        builder.AppendLine(string.Format($"{{0,-26}} {{1,6}} {{2,8}} {{3,6}}", "tool", "desc", "params", "total"));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Format($"{{0,-26}} {{1,6}} {{2,8}} {{3,6}}",
                row.Tool, row.DescriptionChars, row.ParameterChars, row.TotalChars));
        }

        builder.Append(string.Format($"{{0,-26}} {{1,6}} {{2,8}} {{3,6}}",
            $"TOTAL ({rows.Count} tools)", rows.Sum(row => row.DescriptionChars),
            rows.Sum(row => row.ParameterChars), $"{total} / {ceiling}"));
        return builder.ToString();
    }

    private sealed record ToolBudget(string Tool, int DescriptionChars, int ParameterChars)
    {
        public int TotalChars => DescriptionChars + ParameterChars;
    }
}
