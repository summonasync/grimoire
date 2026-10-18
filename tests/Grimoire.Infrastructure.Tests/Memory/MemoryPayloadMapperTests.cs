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
