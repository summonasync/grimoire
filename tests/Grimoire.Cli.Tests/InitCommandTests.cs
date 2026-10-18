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
        _db.CreatedDimensions.ShouldBe(768);
        _db.CreatedIndexes.Count.ShouldBe(7);
        _db.CreatedIndexes.Count(index => index.Type == PayloadSchemaType.Keyword).ShouldBe(6);
        _db.CreatedIndexes.ShouldContain(index => index.Type == PayloadSchemaType.Datetime);
        _db.Upserted.ShouldHaveSingleItem()[0].Payload[EmbeddingFingerprint.ModelField].StringValue.ShouldBe("nomic-embed-text");
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
        _db.CreatedIndexes.Count.ShouldBe(7);
    }

    [Fact]
    public async Task Init_FingerprintMismatch_ExitsDependency()
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

        code.ShouldBe(ExitCodes.Dependency);
        host.Output.ToString().ShouldContain("other-model");
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

    private TestHost NewHost(IReadOnlyDictionary<string, string>? configEnvironment = null) =>
        new(db: _db, configEnvironment: configEnvironment);
}
