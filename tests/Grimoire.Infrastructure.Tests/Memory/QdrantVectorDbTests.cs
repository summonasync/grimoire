using Grimoire.Infrastructure.Memory;
using Grimoire.Infrastructure.Tests.Fakes;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class QdrantVectorDbTests
{
    [Fact]
    public async Task CreateCollection_SendsExplicitNonZeroShardAndReplicationFactors()
    {
        var invoker = new RecordingCallInvoker();
        var db = new QdrantVectorDb(new QdrantClient(new QdrantGrpcClient(invoker)));

        await db.CreateCollectionAsync("grimoire", 768, CancellationToken.None);

        var request = invoker.UnaryRequests.OfType<CreateCollection>().Single();
        request.CollectionName.ShouldBe("grimoire");
        request.ShardNumber.ShouldBe(1u);
        request.ReplicationFactor.ShouldBe(1u);
        request.WriteConsistencyFactor.ShouldBe(1u);
        request.VectorsConfig.Params.Size.ShouldBe(768ul);
        request.VectorsConfig.Params.Distance.ShouldBe(Distance.Cosine);
    }
}
