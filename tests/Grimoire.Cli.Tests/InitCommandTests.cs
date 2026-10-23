using Grimoire.Cli.Tests.Fakes;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Cli.Tests;

public sealed class InitCommandTests : IDisposable
{
    private readonly RecordingVectorDb _db = new();

    [Fact]
    public async Task Init_WritesDefaultConfigWhenMissing()
    {
        using var host = NewHost();

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.Ok);
        File.Exists(host.ConfigPath).ShouldBeTrue();
        var options = new ConfigLoader(host.ConfigPath).Load();
        options.Qdrant.Collection.ShouldBe("grimoire");
        host.Output.ToString().ShouldContain("Wrote default config");
    }

    [Fact]
    public async Task Init_ExistingConfigIsLeftUntouched()
    {
        using var host = NewHost();
        const string custom = "{\n  \"serve\": { \"port\": 5080 }\n}";
        Directory.CreateDirectory(Path.GetDirectoryName(host.ConfigPath)!);
        File.WriteAllText(host.ConfigPath, custom);

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.Ok);
        File.ReadAllText(host.ConfigPath).ShouldBe(custom);
        host.Output.ToString().ShouldContain("left untouched");
    }

    [Fact]
    public async Task Init_BootstrapsFreshCollectionWithIndexesAndMarker()
    {
        using var host = NewHost();

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.Ok);
        _db.CreateCollectionCount.ShouldBe(1);
        _db.CreatedDimensions.ShouldBe(1024);
        _db.CreatedIndexes.Count.ShouldBe(10);
        _db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Keyword).ShouldBe(8);
        _db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Datetime).ShouldBe(2);
        _db.Upserted.ShouldHaveSingleItem()[0].Payload[EmbeddingFingerprint.ModelField].StringValue.ShouldBe("qwen3-embedding");
    }

    [Fact]
    public async Task Init_ExistingCollectionGainsOnlyMissingIndexes()
    {
        _db.CollectionExists = true;
        await _db.CreatePayloadIndexAsync("grimoire", "scope", PayloadSchemaType.Keyword, CancellationToken.None);
        await _db.CreatePayloadIndexAsync("grimoire", "kind", PayloadSchemaType.Keyword, CancellationToken.None);
        using var host = NewHost();

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.Ok);
        _db.CreateCollectionCount.ShouldBe(0);
        _db.CreatedIndexes.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Init_FingerprintMismatch_ExitsReindexRequired()
    {
        _db.CollectionExists = true;
        var marker = new PointStruct { Id = EmbeddingFingerprint.MarkerPointId, Vectors = new float[768] };
        foreach (var pair in EmbeddingFingerprint.From("other-model", 768).ToPayload())
        {
            marker.Payload.Add(pair.Key, pair.Value);
        }

        await _db.UpsertAsync("grimoire", [marker], CancellationToken.None);
        using var host = NewHost();

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.ReindexRequired);
        var output = host.Output.ToString();
        output.ShouldContain("other-model");
        output.ShouldContain("run `grimoire reindex`");
    }

    [Fact]
    public async Task Init_ConfiguredEmbeddingSeedsMatchingFingerprint()
    {
        using var host = NewHost(new Dictionary<string, string>
        {
            ["GRIMOIRE_EMBEDDING__MODEL"] = "bge-m3",
            ["GRIMOIRE_EMBEDDING__DIMENSIONS"] = "1024",
        });

        var code = await host.RunAsync("init");

        code.ShouldBe(ExitCodes.Ok);
        _db.CreatedDimensions.ShouldBe(1024);
        _db.Upserted.ShouldHaveSingleItem()[0].Payload[EmbeddingFingerprint.ModelField].StringValue.ShouldBe("bge-m3");
    }

    public void Dispose()
    {
    }

    [Fact]
    public async Task Init_WithAgent_WritesSingleManagedBlockIntoAgentsFile()
    {
        using var host = NewHost();
        WriteAgentsFile(host, "# Team instructions", "Keep the loopback bind.");

        var code = await host.RunAsync("init", "--agent", "dan", "--tome", "platform-work");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var document = ReadAgentsFile(host);
        document.ShouldContain("# Team instructions");
        document.ShouldContain("Keep the loopback bind.");
        CountMarker(document, ActorClaimBlockParser.StartMarker).ShouldBe(1);
        CountMarker(document, ActorClaimBlockParser.EndMarker).ShouldBe(1);
        document.ShouldContain("alias: dan");
        document.ShouldContain("tome: platform-work");
        ActorClaimBlockParser.Parse(document)!.Tome.ShouldBe("platform-work");
    }

    [Fact]
    public async Task Init_AgentRerun_RewritesTheSameBlockInPlace()
    {
        using var host = NewHost();
        WriteAgentsFile(host, "# Team instructions", "Keep the loopback bind.");
        await host.RunAsync("init", "--agent", "dan", "--tome", "platform-work");

        var code = await host.RunAsync("init", "--agent", "eve", "--tome", "research");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var document = ReadAgentsFile(host);
        document.ShouldContain("# Team instructions");
        document.ShouldNotContain("dan");
        document.ShouldNotContain("platform-work");
        document.ShouldContain("alias: eve");
        document.ShouldContain("tome: research");
        CountMarker(document, ActorClaimBlockParser.StartMarker).ShouldBe(1);
        CountMarker(document, ActorClaimBlockParser.EndMarker).ShouldBe(1);
    }

    [Fact]
    public async Task Init_AgentRerunWithSameValues_IsUpToDate()
    {
        using var host = NewHost();

        await host.RunAsync("init", "--agent", "dan", "--tome", "platform-work");
        var first = ReadAgentsFile(host);
        var code = await host.RunAsync("init", "--agent", "dan", "--tome", "platform-work");

        code.ShouldBe(ExitCodes.Ok);
        ReadAgentsFile(host).ShouldBe(first);
        host.Output.ToString().ShouldContain("already up to date");
    }

    [Fact]
    public async Task Init_AgentWithoutTome_WritesAliasOnlyClaim()
    {
        using var host = NewHost();

        var code = await host.RunAsync("init", "--agent", "dan");

        code.ShouldBe(ExitCodes.Ok, host.Output.ToString());
        var document = ReadAgentsFile(host);
        document.ShouldContain("alias: dan");
        document.ShouldNotContain("tome:");
        ActorClaimBlockParser.Parse(document)!.Alias.ShouldBe("dan");
    }

    [Fact]
    public async Task Init_InvalidTome_LeavesAgentsFileUntouched()
    {
        using var host = NewHost();
        WriteAgentsFile(host, "# Team instructions");

        var code = await host.RunAsync("init", "--agent", "dan", "--tome", "Not Valid!");

        code.ShouldBe(ExitCodes.Usage);
        ReadAgentsFile(host).ShouldNotContain(ActorClaimBlockParser.StartMarker);
    }

    [Fact]
    public async Task Init_TomeWithoutAgent_FailsWithUsage()
    {
        using var host = NewHost();

        var code = await host.RunAsync("init", "--tome", "research");

        code.ShouldBe(ExitCodes.Usage);
        host.Output.ToString().ShouldContain("--tome only applies together with --agent");
    }

    private static string ReadAgentsFile(TestHost host) =>
        File.ReadAllText(Path.Combine(host.Env.WorkingDirectory, ActorClaimProvider.FileName));

    private static void WriteAgentsFile(TestHost host, params string[] lines)
    {
        Directory.CreateDirectory(host.Env.WorkingDirectory);
        File.WriteAllText(Path.Combine(host.Env.WorkingDirectory, ActorClaimProvider.FileName), string.Join("\n", lines) + "\n");
    }

    private static int CountMarker(string document, string marker)
    {
        var count = 0;
        var index = 0;
        while ((index = document.IndexOf(marker, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += marker.Length;
        }

        return count;
    }

    private TestHost NewHost(IReadOnlyDictionary<string, string>? configEnvironment = null) =>
        new(db: _db, configEnvironment: configEnvironment);
}
