using System.Security.Cryptography;
using System.Text;
using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

// Hashes of the protobuf text dump each shape produced on main (55968aa) before the status rule
// moved into Core. They exist to prove the refactor emitted the same bytes, and to stay as a pin:
// a future change to the emitted condition is a decision with a name, not a silent drift.
public sealed class MemoryFilterTranslatorWireShapeTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Until = new(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("empty", "9F071D13EFB3486448C9B603CCE9E95E21871C6A665D9A59EBADEF09A711616B")]
    [InlineData("status-superseded", "FC1D22F6AE68EBD2E7FF28899FCC2EDD517B60826DEC26767A88C77BBDD80E14")]
    [InlineData("status-active", "9F071D13EFB3486448C9B603CCE9E95E21871C6A665D9A59EBADEF09A711616B")]
    [InlineData("statuses-one", "BED8171BE86723980A8E87A7341621AF0FAE4BB9FCE7C2AB5FBC4F835BB5B985")]
    [InlineData("statuses-three", "A0143DE66B1B2668F03564AC0E85047618098FF3E42014712EF671A0524F5973")]
    [InlineData("both-clauses", "EF0E36735A2212CB6F0FA1044E71FCB9408BE62DE8004BB6BBD883BB6988E507")]
    [InlineData("window", "97C9DFCBC7594B6E3DE09B06FED71B5EB3F1E310E7F9F0FDC43BCE86840E9178")]
    [InlineData("scopes-kind-tome", "8BA00D9759C188769247A50E40E4AC11C9828465A9952A5B8F841E01DDCE975B")]
    [InlineData("expiry-tombstone", "2DB8DF166BBEACA1184D45CF31E47D08BB39CA3788B0EE7697CAAF7B481879DB")]
    [InlineData("idempotency-limit", "7AC272CABC61690816A69B0FA1DE7BC92C09F945F0787FD405A792C20794461E")]
    public void Translate_MatchesTheWireShapeRecordedOnMain(string shape, string expectedHash)
    {
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Dump(Shape(shape)))))
            .ShouldBe(expectedHash);
    }

    [Fact]
    public void Translate_ActorIsNoLongerInvisibleOnTheWire()
    {
        var withActor = Dump(new MemoryFilter { Actor = "kelthuzad+qwen" });

        // The sibling of this test asserted the opposite — that an actor candidate dumped identically
        // to an empty filter — while the translator dropped the dimension and recall papered over it.
        // The ten goldens above still pin every other dimension at main's exact bytes, so this change
        // is visible as the actor clause and nothing else.
        withActor.ShouldNotBe(Dump(new MemoryFilter()));
        withActor.ShouldContain("actor.alias");
        withActor.ShouldContain("actor.vendor");
    }

    private static MemoryFilter Shape(string name) => name switch
    {
        "empty" => new MemoryFilter(),
        "status-superseded" => new MemoryFilter { Status = MemoryStatus.Superseded },
        "status-active" => new MemoryFilter { Status = MemoryStatus.Active },
        "statuses-one" => new MemoryFilter { Statuses = [MemoryStatus.Active] },
        "statuses-three" => new MemoryFilter { Statuses = MemoryStatuses.All },
        "both-clauses" => new MemoryFilter
        {
            Status = MemoryStatus.Superseded,
            Statuses = [MemoryStatus.Active, MemoryStatus.Tombstoned],
        },
        "window" => new MemoryFilter { CreatedAfter = From, CreatedBefore = Until },
        "scopes-kind-tome" => new MemoryFilter
        {
            Scopes = [MemoryScope.Session],
            Kind = MemoryKind.Fact,
            Tome = "crew",
        },
        "expiry-tombstone" => new MemoryFilter { ExpiresBefore = Until, TombstonedBefore = Until },
        _ => new MemoryFilter { IdempotencyKey = "key-7", Limit = 5 },
    };

    private static string Dump(MemoryFilter filter) => MemoryFilterTranslator.Translate(filter).ToString();
}
