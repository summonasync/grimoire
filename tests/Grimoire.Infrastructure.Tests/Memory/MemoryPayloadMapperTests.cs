using Grimoire.Core;
using Grimoire.Infrastructure.Memory;
using Qdrant.Client.Grpc;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Memory;

public sealed class MemoryPayloadMapperTests
{
    [Fact]
    public void ToPayload_WithNullOptionalFields_OmitsThemFromPayload()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("plain"));

        payload.ShouldNotContainKey(MemoryPayloadFields.ProjectId);
        payload.ShouldNotContainKey(MemoryPayloadFields.SessionId);
        payload.ShouldNotContainKey(MemoryPayloadFields.ExpiresAt);
        payload.ShouldNotContainKey(MemoryPayloadFields.SupersededBy);
        payload.ShouldNotContainKey(MemoryPayloadFields.TombstoneReason);
        payload.ShouldNotContainKey(MemoryPayloadFields.TombstonedAt);
        payload.ShouldNotContainKey(MemoryPayloadFields.IdempotencyKey);
        payload[MemoryPayloadFields.Status].StringValue.ShouldBe("Active");
        payload[MemoryPayloadFields.Tags].ListValue.Values.Count.ShouldBe(0);
    }

    [Fact]
    public void ToPayload_WithHumanActor_WritesAliasOnly()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("human", actor: MemoryActor.Human));
        var actor = payload[MemoryPayloadFields.Actor].StructValue.Fields;

        actor["alias"].StringValue.ShouldBe("human");
        actor.ShouldNotContainKey("vendor");
    }

    [Fact]
    public void ToPayload_WithIdempotencyKey_WritesItToPayload()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("keyed", idempotencyKey: "key-9"));

        payload[MemoryPayloadFields.IdempotencyKey].StringValue.ShouldBe("key-9");
    }

    [Fact]
    public void ToPayload_WithTome_WritesConcreteValue()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("tomed", tome: "alpha"));

        payload[MemoryPayloadFields.Tome].StringValue.ShouldBe("alpha");
    }

    [Fact]
    public void ToPayload_WithDefaultTome_StillWritesTheField()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("plain"));

        payload[MemoryPayloadFields.Tome].StringValue.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public void ToRecord_WithMissingOptionalFields_UsesDefaults()
    {
        var payload = new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Content] = "body",
            [MemoryPayloadFields.Scope] = "Project",
            [MemoryPayloadFields.Kind] = "Journal",
            [MemoryPayloadFields.Status] = "Active",
            [MemoryPayloadFields.Actor] = Actor("agent", "claude"),
            [MemoryPayloadFields.Importance] = 0.5,
            [MemoryPayloadFields.CreatedAt] = TestMemories.Moment.ToString("O"),
            [MemoryPayloadFields.LastAccessedAt] = TestMemories.Moment.ToString("O"),
        };
        var id = Guid.NewGuid();

        var record = MemoryPayloadMapper.ToRecord(id, payload);

        record.Id.ShouldBe(id);
        record.ProjectId.ShouldBeNull();
        record.SessionId.ShouldBeNull();
        record.Tags.ShouldBe([]);
        record.Sources.ShouldBe([]);
        record.AccessCount.ShouldBe(0);
        record.ExpiresAt.ShouldBeNull();
        record.SupersededBy.ShouldBeNull();
        record.TombstoneReason.ShouldBeNull();
        record.TombstonedAt.ShouldBeNull();
        record.IdempotencyKey.ShouldBeNull();
    }

    [Fact]
    public void ToRecord_WithAbsentTome_DecodesAsDefaultTome()
    {
        var payload = LegacyPayload();

        var record = MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload);

        record.Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public void ToRecord_WithExplicitNullTome_DecodesAsDefaultTome()
    {
        var payload = LegacyPayload();
        payload[MemoryPayloadFields.Tome] = new Value { NullValue = NullValue.NullValue };

        var record = MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload);

        record.Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public void ToRecord_WithNonNormalizedTome_NormalizesTheKey()
    {
        var payload = LegacyPayload();
        payload[MemoryPayloadFields.Tome] = "Shared Library";

        var record = MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload);

        record.Tome.ShouldBe("shared-library");
    }

    [Fact]
    public void ToRecord_WithInvalidTome_ThrowsTypedError()
    {
        var payload = LegacyPayload();
        payload[MemoryPayloadFields.Tome] = "Not A Tome!!";

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("tome");
    }

    [Fact]
    public void ToRecord_AfterToPayload_RoundTripsTheTome()
    {
        var record = TestMemories.Create("round trip", tome: "alpha");

        var decoded = MemoryPayloadMapper.ToRecord(record.Id, new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(record)));

        decoded.Tome.ShouldBe("alpha");
    }

    [Fact]
    public void ToPayload_WithCorrections_WritesStructuredTrail()
    {
        var record = TestMemories.Create("traced") with
        {
            Corrections =
            [
                new CorrectionEntry
                {
                    At = TestMemories.Moment,
                    Actor = MemoryActor.Human,
                    Action = CorrectionAction.Forget,
                    Reason = "wrong info",
                },
            ],
        };

        var payload = MemoryPayloadMapper.ToPayload(record);
        var line = payload[MemoryPayloadFields.Corrections].ListValue.Values.Single().StructValue.Fields;

        line["at"].StringValue.ShouldBe(TestMemories.Moment.ToString("O"));
        line["action"].StringValue.ShouldBe("Forget");
        line["reason"].StringValue.ShouldBe("wrong info");
        line["actor"].StructValue.Fields["alias"].StringValue.ShouldBe("human");
    }

    [Fact]
    public void ToRecord_AfterToPayload_RoundTripsCorrections()
    {
        var record = TestMemories.Create("round trip") with
        {
            Corrections =
            [
                new CorrectionEntry
                {
                    At = TestMemories.Moment,
                    Actor = MemoryActor.Agent("crew", "claude"),
                    Action = CorrectionAction.Restore,
                    Reason = "undo",
                },
                new CorrectionEntry
                {
                    At = TestMemories.Moment.AddMinutes(1),
                    Actor = MemoryActor.Human,
                    Action = CorrectionAction.Supersede,
                    Reason = "restored predecessor",
                },
                new CorrectionEntry
                {
                    At = TestMemories.Moment.AddMinutes(2),
                    Actor = MemoryActor.Human,
                    Action = CorrectionAction.Retome,
                    Reason = "tome: alpha → beta",
                },
            ],
        };

        var decoded = MemoryPayloadMapper.ToRecord(record.Id, new Dictionary<string, Value>(MemoryPayloadMapper.ToPayload(record)));

        decoded.Corrections.ShouldBe(record.Corrections);
    }

    [Fact]
    public void ToRecord_WithMissingCorrections_DecodesEmptyTrail()
    {
        var payload = LegacyPayload();

        var decoded = MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload);

        decoded.Corrections.ShouldBe([]);
    }

    [Fact]
    public void ToRecord_WithUnknownCorrectionAction_ThrowsTypedError()
    {
        var payload = FullPayload();
        payload[MemoryPayloadFields.Corrections] = new Value
        {
            ListValue = new ListValue { Values = { new Value { StringValue = "not a struct" } } },
        };

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("corrections");
    }

    [Fact]
    public void ToRecord_WithMissingRequiredField_ThrowsTypedError()
    {
        var payload = new Dictionary<string, Value>
        {
            [MemoryPayloadFields.Scope] = "Project",
            [MemoryPayloadFields.Kind] = "Journal",
            [MemoryPayloadFields.Status] = "Active",
            [MemoryPayloadFields.Actor] = Actor("agent", null),
            [MemoryPayloadFields.Importance] = 0.5,
            [MemoryPayloadFields.CreatedAt] = TestMemories.Moment.ToString("O"),
            [MemoryPayloadFields.LastAccessedAt] = TestMemories.Moment.ToString("O"),
        };

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("content");
    }

    [Fact]
    public void ToRecord_WithUnknownEnumValue_ThrowsTypedError()
    {
        var payload = FullPayload();
        payload[MemoryPayloadFields.Scope] = "Galactic";

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("Galactic");
    }

    [Fact]
    public void ToRecord_WithMalformedTimestamp_ThrowsTypedError()
    {
        var payload = FullPayload();
        payload[MemoryPayloadFields.CreatedAt] = "not-a-date";

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("timestamp");
    }

    [Fact]
    public void ToRecord_WithCorruptSourceGuid_ThrowsTypedError()
    {
        var payload = FullPayload();
        payload[MemoryPayloadFields.Sources] = new[] { "not-a-guid" };

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload));
    }

    [Fact]
    public void ToRecord_WithNonStringTag_ThrowsTypedError()
    {
        var payload = FullPayload();
        payload[MemoryPayloadFields.Tags] = new Value { ListValue = new ListValue { Values = { 42 } } };

        Should.Throw<PayloadDecodeException>(() => MemoryPayloadMapper.ToRecord(Guid.NewGuid(), payload))
            .Message.ShouldContain("tags");
    }

    private static Dictionary<string, Value> FullPayload()
    {
        var payload = MemoryPayloadMapper.ToPayload(TestMemories.Create("body", projectId: "p1"));
        return new Dictionary<string, Value>(payload);
    }

    private static Dictionary<string, Value> LegacyPayload()
    {
        var payload = FullPayload();
        payload.Remove(MemoryPayloadFields.Tome);
        return payload;
    }

    private static Value Actor(string alias, string? vendor)
    {
        var fields = new Struct();
        fields.Fields.Add("alias", alias);

        if (vendor is not null)
        {
            fields.Fields.Add("vendor", vendor);
        }

        return new Value { StructValue = fields };
    }
}
