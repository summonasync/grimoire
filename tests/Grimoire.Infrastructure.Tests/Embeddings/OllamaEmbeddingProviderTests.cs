using System.Net;
using Grimoire.Infrastructure.Configuration;
using Grimoire.Infrastructure.Embeddings;
using Grimoire.Infrastructure.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Embeddings;

public sealed class OllamaEmbeddingProviderTests
{
    [Fact]
    public async Task Embed_WithSuccessfulResponse_ReturnsVector()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("""{"embedding":[0.25,0.75]}"""));
        var provider = Provider(handler);

        var vector = await provider.EmbedAsync("hello", CancellationToken.None);

        vector.ToArray().ShouldBe(new float[] { 0.25f, 0.75f });
        handler.Requests.ShouldHaveSingleItem().RequestUri!.ToString().ShouldBe("http://ollama.test:11434/api/embeddings");
        var body = handler.Bodies[0];
        body.ShouldNotBeNull();
        body.ShouldContain("\"model\":\"test-model\"");
        body.ShouldContain("\"prompt\":\"hello\"");
    }

    [Fact]
    public async Task Embed_WhenEndpointUnreachable_ThrowsTypedUnavailableError()
    {
        var handler = new FakeOllamaHandler(_ => throw new HttpRequestException("connection refused", inner: null));
        var provider = Provider(handler);

        var exception = await Should.ThrowAsync<EmbeddingUnavailableException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));

        exception.Message.ShouldContain("unreachable");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task Embed_WhenRequestTimesOut_ThrowsTypedUnavailableError()
    {
        var handler = new FakeOllamaHandler(_ => throw new TaskCanceledException("timed out", new TimeoutException()));
        var provider = Provider(handler);

        var exception = await Should.ThrowAsync<EmbeddingUnavailableException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));

        exception.Message.ShouldContain("timed out");
        exception.InnerException.ShouldBeOfType<TaskCanceledException>();
    }

    [Fact]
    public async Task Embed_WhenCallerCancels_PropagatesCancellation()
    {
        var handler = new FakeOllamaHandler(_ => throw new TaskCanceledException("cancelled by caller"));
        var provider = Provider(handler);

        await Should.ThrowAsync<OperationCanceledException>(
            () => provider.EmbedAsync("hello", new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Embed_WithHttpErrorStatus_ThrowsTypedResponseError()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("model not found", HttpStatusCode.InternalServerError));
        var provider = Provider(handler);

        var exception = await Should.ThrowAsync<EmbeddingResponseException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));

        exception.Message.ShouldContain("500");
    }

    [Fact]
    public async Task Embed_WithMalformedJson_ThrowsTypedResponseError()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("<not json>"));
        var provider = Provider(handler);

        await Should.ThrowAsync<EmbeddingResponseException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));
    }

    [Fact]
    public async Task Embed_WithMissingEmbeddingArray_ThrowsTypedResponseError()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("{}"));
        var provider = Provider(handler);

        await Should.ThrowAsync<EmbeddingResponseException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));
    }

    [Fact]
    public async Task Embed_WithEmptyEmbeddingArray_ThrowsTypedResponseError()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("""{"embedding":[]}"""));
        var provider = Provider(handler);

        await Should.ThrowAsync<EmbeddingResponseException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));
    }

    [Fact]
    public async Task Embed_WithDimensionMismatch_ThrowsTypedResponseError()
    {
        var handler = new FakeOllamaHandler(_ => FakeOllamaHandler.Json("""{"embedding":[0.1]}"""));
        var provider = Provider(handler);

        var exception = await Should.ThrowAsync<EmbeddingResponseException>(
            () => provider.EmbedAsync("hello", CancellationToken.None));

        exception.Message.ShouldContain("dimensions");
    }

    private static OllamaEmbeddingProvider Provider(FakeOllamaHandler handler) =>
        new(new HttpClient(handler), new EmbeddingOptions
        {
            Endpoint = "http://ollama.test:11434",
            Model = "test-model",
            Dimensions = 2,
        });
}
