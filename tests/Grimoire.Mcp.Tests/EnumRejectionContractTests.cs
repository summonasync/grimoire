using System.Text.Json;
using Grimoire.Core;
using ModelContextProtocol.Client;

namespace Grimoire.Mcp.Tests;

public sealed class EnumRejectionContractTests
{
    private static readonly string[] ScopeTokens = ["Session", "Project", "Global", "Personal"];

    public static TheoryData<string, string, string[], string> EnumSites()
    {
        var data = new TheoryData<string, string, string[], string>();
        data.Add("memory_list", "scope", ScopeTokens, "global");
        data.Add("memory_list", "kind", new[] { "Journal", "Fact" }, "fact");
        data.Add("memory_list", "status", new[] { "Active", "Superseded", "Tombstoned" }, "active");
        data.Add("memory_recall", "scope", ScopeTokens, "global");
        data.Add("memory_recall", "kind", new[] { "Journal", "Fact" }, "fact");
        data.Add("memory_remember", "scope", ScopeTokens, "global");
        data.Add("memory_remember", "kind", new[] { "Journal", "Fact" }, "fact");
        data.Add("memory_scry", "attribution", new[] { "None", "Light", "Full" }, "light");
        data.Add("memory_brief", "scopes", ScopeTokens, "global");
        return data;
    }

    private static Dictionary<string, object?> Args(string tool, string param, string value) => tool switch
    {
        "memory_recall" => new Dictionary<string, object?> { ["query"] = "enum rejection contract", [param] = value },
        "memory_scry" => new Dictionary<string, object?> { ["query"] = "enum rejection contract", [param] = value },
        "memory_remember" => new Dictionary<string, object?>
        {
            ["content"] = "enum rejection contract probe",
            ["scope"] = param == "scope" ? value : "Global",
            ["kind"] = param == "kind" ? value : "Journal",
        },
        "memory_brief" => new Dictionary<string, object?> { [param] = new[] { value } },
        _ => new Dictionary<string, object?> { [param] = value },
    };

    public static TheoryData<string, string, string[]> EnumSiteTokens()
    {
        var data = new TheoryData<string, string, string[]>();
        data.Add("memory_list", "scope", ScopeTokens);
        data.Add("memory_list", "kind", new[] { "Journal", "Fact" });
        data.Add("memory_list", "status", new[] { "Active", "Superseded", "Tombstoned" });
        data.Add("memory_recall", "scope", ScopeTokens);
        data.Add("memory_recall", "kind", new[] { "Journal", "Fact" });
        data.Add("memory_remember", "scope", ScopeTokens);
        data.Add("memory_remember", "kind", new[] { "Journal", "Fact" });
        data.Add("memory_scry", "attribution", new[] { "None", "Light", "Full" });
        data.Add("memory_brief", "scopes", ScopeTokens);
        return data;
    }

    [Theory]
    [MemberData(nameof(EnumSiteTokens))]
    public async Task EveryLowercaseTokenVariant_RejectedWithCanonicalTeaching(string tool, string param, string[] canonical)
    {
        await using var session = await McpTestSession.StartAsync();

        foreach (var token in canonical)
        {
            var lowercase = token.ToLowerInvariant();

            var result = await session.Client.CallToolAsync(tool, Args(tool, param, lowercase));

            var (code, message, failedParam) = result.Error();
            code.ShouldBe(ToolErrorCodes.InvalidRequest, customMessage: $"'{tool}.{param}={lowercase}' must hit the enum guard");
            failedParam.ShouldBe(param);
            message.ShouldContain($"'{lowercase}'");
            message.ShouldContain("case-sensitive", Case.Sensitive);
            foreach (var advertised in canonical)
            {
                message.ShouldContain(advertised, Case.Sensitive);
            }

            if (tool == "memory_remember")
            {
                message.ShouldContain("Nothing was applied by this call.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(EnumSites))]
    public async Task WrongCaseEnumValue_RejectionNamesParamEchoesValueListsTokensStatesCaseRule(
        string tool, string param, string[] canonical, string wrongCase)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync(tool, Args(tool, param, wrongCase));

        var (code, message, failedParam) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        failedParam.ShouldBe(param);
        message.ShouldContain($"'{wrongCase}'");
        message.ShouldContain("case-sensitive");
        foreach (var token in canonical)
        {
            message.ShouldContain(token, Case.Sensitive);
        }

        if (tool == "memory_remember")
        {
            message.ShouldContain("Nothing was applied by this call.");
        }
    }

    [Theory]
    [MemberData(nameof(EnumSites))]
    public async Task UnknownEnumToken_RejectionCarriesTheSameTeachingContract(
        string tool, string param, string[] canonical, string _)
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync(tool, Args(tool, param, "zzz-not-a-token"));

        var (code, message, failedParam) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        failedParam.ShouldBe(param);
        message.ShouldContain("'zzz-not-a-token'");
        foreach (var token in canonical)
        {
            message.ShouldContain(token, Case.Sensitive);
        }
    }

    [Theory]
    [MemberData(nameof(EnumSites))]
    public async Task EveryAdvertisedToken_Parses(string tool, string param, string[] canonical, string _)
    {
        await using var session = await McpTestSession.StartAsync();

        foreach (var token in canonical)
        {
            var result = await session.Client.CallToolAsync(tool, Args(tool, param, token));

            if (result.IsError ?? false)
            {
                var (_, message, failedParam) = result.Error();
                (failedParam == param && message.Contains("is not an allowed value", StringComparison.Ordinal))
                    .ShouldBeFalse($"advertised token '{token}' was rejected at {tool}.{param}: {message}");
            }
        }
    }

    [Fact]
    public async Task MistypedEnumWrite_ReachesNoStoreAndStatesNothingApplied()
    {
        await using var session = await McpTestSession.StartAsync();

        var result = await session.Client.CallToolAsync(
            "memory_remember",
            new Dictionary<string, object?>
            {
                ["content"] = "mistyped enum write must not persist",
                ["scope"] = "global",
            });

        var (code, message, param) = result.Error();
        code.ShouldBe(ToolErrorCodes.InvalidRequest);
        param.ShouldBe("scope");
        message.ShouldContain("Nothing was applied by this call.");
        session.Store.Points.ShouldBeEmpty();
        session.Embeddings.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void SiteTokenLists_MirrorTheCanonicalEnumNames()
    {
        ScopeTokens.ShouldBe(Enum.GetNames<MemoryScope>());
        Enum.GetNames<MemoryKind>().ShouldBe(["Journal", "Fact"]);
        Enum.GetNames<MemoryStatus>().ShouldBe(["Active", "Superseded", "Tombstoned"]);
        Enum.GetNames<ScryAttribution>().ShouldBe(["None", "Light", "Full"]);
    }

    [Fact]
    public async Task CrossParamGuards_NameTheOffendingParamAndEchoTheGivenValue()
    {
        await using var session = await McpTestSession.StartAsync();

        var conflict = await session.Client.CallToolAsync(
            "memory_remember",
            new Dictionary<string, object?>
            {
                ["content"] = "guard echo probe",
                ["scope"] = "Global",
                ["projectId"] = "grimoire",
            });

        var (code, message, param) = conflict.Error();
        code.ShouldBe(ToolErrorCodes.ParamConflict);
        param.ShouldBe("projectId");
        message.ShouldContain("Project");
        message.ShouldContain("'grimoire'");
        message.ShouldContain("'Global'");
        message.ShouldContain("Nothing was applied by this call.");
    }
}
