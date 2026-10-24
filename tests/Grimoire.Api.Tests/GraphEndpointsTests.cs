using System.Net;
using System.Text.Json;
using Grimoire.Api.Contracts;
using Grimoire.Api.Tests.Fakes;
using Grimoire.Core;
using Shouldly;
using Xunit;

namespace Grimoire.Api.Tests;

public class GraphEndpointsTests(GrimoireApiFactory factory) : IClassFixture<GrimoireApiFactory>
{
    [Fact]
    public async Task Related_DefaultClasses_ReturnsClassifiedNeighbors()
    {
        factory.Reset();
        var target = factory.Store.Seed("target note", [1f, 0f], tags: ["team"]);
        var linked = factory.Store.Seed($"links back [[{target.Id}]]", [0f, 1f]);
        var older = factory.Store.Seed("older note", [0f, 0f, 1f], status: MemoryStatus.Superseded, supersededBy: target.Id);
        var twin = factory.Store.Seed("twin note", [1f, 0f]);
        factory.Store.Seed("stranger note", [0f, 1f], tags: ["team"]);

        var related = await ReadRelated(await factory.NewClient().GetAsync($"/api/memories/{target.Id}/related"));

        related.Id.ShouldBe(target.Id);
        related.Tome.ShouldBe(TomeKey.DefaultTome);
        related.Edges.Select(edge => edge.Class)
            .ShouldBe(new[] { RecordEdgeClass.Supersede, RecordEdgeClass.Link, RecordEdgeClass.Similarity }, ignoreOrder: true);

        var supersede = related.Edges.Single(edge => edge.Class == RecordEdgeClass.Supersede);
        supersede.Direction.ShouldBe(GraphDirection.Inbound);
        supersede.Target.Id.ShouldBe(older.Id);
        supersede.Target.Status.ShouldBe(MemoryStatus.Superseded);

        var link = related.Edges.Single(edge => edge.Class == RecordEdgeClass.Link);
        link.Direction.ShouldBe(GraphDirection.Inbound);
        link.Target.Id.ShouldBe(linked.Id);
        link.Target.Status.ShouldBe(MemoryStatus.Active);

        var similarity = related.Edges.Single(edge => edge.Class == RecordEdgeClass.Similarity);
        var expectedDirection = target.Id.CompareTo(twin.Id) < 0 ? GraphDirection.Outbound : GraphDirection.Inbound;
        similarity.Direction.ShouldBe(expectedDirection);
        similarity.Target.Id.ShouldBe(twin.Id);
        similarity.Target.Tome.ShouldBe(TomeKey.DefaultTome);
    }

    [Fact]
    public async Task Related_WithAbSubset_OmitsSimilarityAndCoMembers()
    {
        factory.Reset();
        var target = factory.Store.Seed("target note", [1f, 0f], tags: ["team"]);
        var linked = factory.Store.Seed($"linked note [[{target.Id}]]", [0f, 1f]);
        var older = factory.Store.Seed("older note", [0f, 0f, 1f], status: MemoryStatus.Superseded, supersededBy: target.Id);
        var twin = factory.Store.Seed("twin note", [1f, 0f]);

        var related = await ReadRelated(await factory.NewClient()
            .GetAsync($"/api/memories/{target.Id}/related?classes=a,b"));

        related.Edges.Select(edge => edge.Class)
            .ShouldBe(new[] { RecordEdgeClass.Supersede, RecordEdgeClass.Link }, ignoreOrder: true);
        related.Edges.Select(edge => edge.Target.Id).ShouldBe(new[] { older.Id, linked.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task Related_WithCSubset_ReturnsOnlySimilarity()
    {
        factory.Reset();
        var target = factory.Store.Seed("target note", [1f, 0f]);
        var twin = factory.Store.Seed("twin note", [1f, 0f]);

        var related = await ReadRelated(await factory.NewClient()
            .GetAsync($"/api/memories/{target.Id}/related?classes=c"));

        var edge = related.Edges.ShouldHaveSingleItem();
        edge.Class.ShouldBe(RecordEdgeClass.Similarity);
        edge.Target.Id.ShouldBe(twin.Id);
    }

    [Fact]
    public async Task Related_WithD_IncludesCoMembers()
    {
        factory.Reset();
        var target = factory.Store.Seed("target note", [1f, 0f], tags: ["crew"]);
        var member = factory.Store.Seed("member note", [0f, 1f], tags: ["crew"]);

        var related = await ReadRelated(await factory.NewClient()
            .GetAsync($"/api/memories/{target.Id}/related?classes=d"));

        var edge = related.Edges.ShouldHaveSingleItem();
        edge.Class.ShouldBe(RecordEdgeClass.CoMembership);
        edge.Target.Id.ShouldBe(member.Id);
    }

    [Fact]
    public async Task Related_TombstonedTarget_CarriesHonestStatus()
    {
        factory.Reset();
        var gone = factory.Store.Seed(
            "gone note",
            [0f, 1f],
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "cleaned up");
        var mourner = factory.Store.Seed($"mourner note [[{gone.Id}]]", [1f, 0f]);

        var related = await ReadRelated(await factory.NewClient().GetAsync($"/api/memories/{mourner.Id}/related"));

        var edge = related.Edges.ShouldHaveSingleItem();
        edge.Class.ShouldBe(RecordEdgeClass.Link);
        edge.Direction.ShouldBe(GraphDirection.Outbound);
        edge.Target.Id.ShouldBe(gone.Id);
        edge.Target.Status.ShouldBe(MemoryStatus.Tombstoned);
    }

    [Fact]
    public async Task Related_CrossTomeNeighbor_NeverAppears()
    {
        factory.Reset();
        var target = factory.Store.Seed("target note", [1f, 0f]);
        factory.Store.Seed("foreign twin", [1f, 0f], tome: "elsewhere");

        var related = await ReadRelated(await factory.NewClient().GetAsync($"/api/memories/{target.Id}/related"));

        related.Edges.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("z")]
    [InlineData("a;e")]
    public async Task Related_WithUnknownClass_Returns400(string classes)
    {
        factory.Reset();

        var response = await factory.NewClient()
            .GetAsync($"/api/memories/{Guid.NewGuid()}/related?classes={Uri.EscapeDataString(classes)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var (code, message) = await ReadError(response);
        code.ShouldBe("invalid_class");
        message.ShouldContain(classes.Split(',')[0]);
    }

    [Fact]
    public async Task Related_UnknownRecord_Returns404()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/memories/{Guid.NewGuid()}/related");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadError(response)).code.ShouldBe("not_found");
    }

    [Fact]
    public async Task Related_WithCustomThreshold_ExcludesBelowThreshold()
    {
        using var strictFactory = GrimoireApiFactory.Create(new MemoryEngineOptions { RelatedSimilarityThreshold = 0.95 });
        strictFactory.Reset();
        var target = strictFactory.Store.Seed("target note", [1f, 0f]);
        strictFactory.Store.Seed("near twin", [0.9f, 0.435889f]);

        var related = await ReadRelated(await strictFactory.NewClient().GetAsync($"/api/memories/{target.Id}/related"));

        related.Edges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Related_LongMultilineNeighbor_FlattensAndTruncatesSnippet()
    {
        factory.Reset();
        var neighbor = factory.Store.Seed(new string('x', 130) + "\nsecond line", [1f, 0f]);
        var anchor = factory.Store.Seed($"see [[{neighbor.Id}]]", [0f, 1f]);

        var related = await ReadRelated(await factory.NewClient().GetAsync($"/api/memories/{anchor.Id}/related"));

        var edge = related.Edges.ShouldHaveSingleItem();
        edge.Target.Snippet.ShouldBe(new string('x', 120) + "...");
    }

    [Fact]
    public async Task Graph_DefaultClasses_ShowsActiveNodesAndHonestEdges()
    {
        factory.Reset();
        var active = factory.Store.Seed("alpha one", [1f, 0f], tome: "alpha", kind: MemoryKind.Fact);
        var second = factory.Store.Seed($"alpha two [[{active.Id}]]", [1f, 0f], tome: "alpha");
        var projectMember = TestMemories.Create("alpha project note", tome: "alpha") with { ProjectId = "grimoire" };
        factory.Store.Seed(projectMember, [0f, 1f]);
        var old = factory.Store.Seed("alpha old", [0f, 0f, 1f], tome: "alpha", status: MemoryStatus.Superseded, supersededBy: active.Id);
        factory.Store.Seed("alpha ghost", [0f, 1f, 0f], tome: "alpha", status: MemoryStatus.Tombstoned, tombstoneReason: "cleaned");

        var response = await factory.NewClient().GetAsync("/api/tomes/alpha/graph");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var graph = await ReadGraph(response);
        graph.Tome.ShouldBe("alpha");

        graph.Nodes.Select(node => node.Id).ShouldBe(new[] { active.Id, second.Id, projectMember.Id }, ignoreOrder: true);
        graph.Nodes.ShouldNotContain(node => node.Id == old.Id);

        var activeNode = graph.Nodes.Single(node => node.Id == active.Id);
        activeNode.Snippet.ShouldBe("alpha one");
        activeNode.Kind.ShouldBe(MemoryKind.Fact);
        activeNode.Project.ShouldBeNull();

        graph.Nodes.Single(node => node.Id == projectMember.Id).Project.ShouldBe("grimoire");

        var supersede = graph.Edges.Single(edge => edge.Class == RecordEdgeClass.Supersede);
        supersede.From.ShouldBe(old.Id);
        supersede.To.ShouldBe(active.Id);
        supersede.TargetStatus.ShouldBe(MemoryStatus.Active);

        var link = graph.Edges.Single(edge => edge.Class == RecordEdgeClass.Link);
        link.TargetStatus.ShouldBe(MemoryStatus.Active);
        graph.Edges.ShouldContain(edge => edge.Class == RecordEdgeClass.Similarity);
        graph.Edges.ShouldNotContain(edge => edge.Class == RecordEdgeClass.CoMembership);
    }

    [Fact]
    public async Task Graph_WithCSubset_ReturnsOnlySimilarityEdges()
    {
        factory.Reset();
        var target = factory.Store.Seed("alpha target", [1f, 0f], tome: "alpha");
        var twin = factory.Store.Seed("alpha twin", [1f, 0f], tome: "alpha");
        var linked = factory.Store.Seed($"alpha link [[{target.Id}]]", [0f, 1f], tome: "alpha");

        var graph = await ReadGraph(await factory.NewClient().GetAsync("/api/tomes/alpha/graph?classes=c"));

        graph.Edges.Select(edge => edge.Class).ShouldBe(new[] { RecordEdgeClass.Similarity }, ignoreOrder: true);
        graph.Edges.ShouldContain(edge => edge.From == target.Id || edge.To == target.Id);
        graph.Nodes.Select(node => node.Id).ShouldBe(new[] { target.Id, twin.Id, linked.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task Graph_WithD_IncludesCoMembershipEdges()
    {
        factory.Reset();
        var target = factory.Store.Seed("alpha target", [1f, 0f], tome: "alpha", tags: ["crew"]);
        var member = factory.Store.Seed("alpha member", [0f, 1f], tome: "alpha", tags: ["crew"]);

        var graph = await ReadGraph(await factory.NewClient().GetAsync("/api/tomes/alpha/graph?classes=d"));

        var edge = graph.Edges.ShouldHaveSingleItem();
        edge.Class.ShouldBe(RecordEdgeClass.CoMembership);
        edge.From.ShouldBe(target.Id.CompareTo(member.Id) < 0 ? target.Id : member.Id);
        graph.Nodes.Select(node => node.Id).ShouldBe(new[] { target.Id, member.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task Graph_WithUnknownClass_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync("/api/tomes/alpha/graph?classes=nope");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_class");
    }

    [Fact]
    public async Task Graph_WithInvalidTome_Returns400()
    {
        factory.Reset();

        var response = await factory.NewClient().GetAsync($"/api/tomes/{Uri.EscapeDataString("Not Valid!!")}/graph");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadError(response)).code.ShouldBe("invalid_tome");
    }

    [Fact]
    public async Task Graph_UnknownTome_ReturnsEmptyGraph()
    {
        factory.Reset();
        factory.Store.Seed("home note", [1f, 0f]);

        var graph = await ReadGraph(await factory.NewClient().GetAsync("/api/tomes/nowhere/graph"));

        graph.Tome.ShouldBe("nowhere");
        graph.Nodes.ShouldBeEmpty();
        graph.Edges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Graph_Routes_FetchTomeWithExplicitAllStatuses()
    {
        factory.Reset();
        var gone = factory.Store.Seed(
            "gone note",
            [0f, 1f],
            tome: "alpha",
            status: MemoryStatus.Tombstoned,
            tombstoneReason: "cleaned");
        var mourner = factory.Store.Seed($"mourner note [[{gone.Id}]]", [1f, 0f], tome: "alpha");
        var filters = new List<MemoryFilter>();
        factory.Store.OnFetchAsync = filters.Add;

        var graph = await ReadGraph(await factory.NewClient().GetAsync("/api/tomes/alpha/graph"));
        var related = await ReadRelated(await factory.NewClient().GetAsync($"/api/memories/{mourner.Id}/related"));

        filters.Count.ShouldBe(2);
        foreach (var filter in filters)
        {
            filter.Tome.ShouldBe("alpha");
            filter.Statuses.ShouldBe(
                [MemoryStatus.Active, MemoryStatus.Superseded, MemoryStatus.Tombstoned],
                ignoreOrder: true);
        }

        graph.Nodes.ShouldNotContain(node => node.Id == gone.Id);
        graph.Edges.ShouldContain(edge => edge.From == mourner.Id && edge.To == gone.Id);
        related.Edges.Single(edge => edge.Class == RecordEdgeClass.Link).Target.Id.ShouldBe(gone.Id);
    }

    private static async Task<RelatedMemoryResponse> ReadRelated(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<RelatedMemoryResponse>(stream, TestJson.Options))!;
    }

    private static async Task<TomeGraphResponse> ReadGraph(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<TomeGraphResponse>(stream, TestJson.Options))!;
    }

    private static async Task<(string code, string message)> ReadError(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        var body = (await JsonSerializer.DeserializeAsync<JsonElement>(stream, TestJson.Options))!;
        var error = body.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }
}
