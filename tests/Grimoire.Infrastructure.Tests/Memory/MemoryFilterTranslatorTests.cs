using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class MemoryFilterTranslatorTests
{
    [Fact]
    public void Translate_Always_ConstrainsStatusToActive()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter());

        var status = filter.Must.ShouldHaveSingleItem().Field!;
        status.Key.ShouldBe(MemoryPayloadFields.Status);
        status.Match!.Keyword.ShouldBe("Active");
    }

    [Fact]
    public void Translate_WithIdempotencyKey_AddsKeywordEquality()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { IdempotencyKey = "key-7" });

        var condition = filter.Must.Single(item => item.Field?.Key == MemoryPayloadFields.IdempotencyKey).Field!;
        condition.Match!.Keyword.ShouldBe("key-7");
    }

    [Fact]
    public void Translate_WithCreatedAfter_AddsInclusiveLowerBoundOnCreatedAt()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter { CreatedAfter = TestMemories.Moment });

        var range = filter.Must.Single(item => item.Field?.Key == MemoryPayloadFields.CreatedAt).Field!.DatetimeRange!;
        range.Gte!.ToDateTime().ShouldBe(TestMemories.Moment.UtcDateTime);
    }

    [Fact]
    public void Translate_WithReplayLookupShape_ConstrainsStatusKeyAndWindowTogether()
    {
        var filter = MemoryFilterTranslator.Translate(new MemoryFilter
        {
            IdempotencyKey = "key-7",
            CreatedAfter = TestMemories.Moment,
        });

        filter.Must.Count.ShouldBe(3);
        filter.Must.Select(item => item.Field!.Key).ShouldBe(
            [MemoryPayloadFields.Status, MemoryPayloadFields.IdempotencyKey, MemoryPayloadFields.CreatedAt]);
    }
}
