using System.Net;
using Grimoire.Core;
using Grimoire.Infrastructure.Board;
using Grimoire.Infrastructure.Tests.Fakes;
using Shouldly;
using Xunit;

namespace Grimoire.Infrastructure.Tests.Board;

public sealed class OvermindBoardProjectSourceTests
{
    private const string Payload = """
        [
          { "id": "7450aeab", "name": "Grimoire", "diskPath": "/mnt/Projects/Grimoire", "description": "memory" },
          { "id": "9afb1c", "name": "Squeezr", "diskPath": null, "description": "" }
        ]
        """;

    [Fact]
    public async Task Fetch_ParsesTheProjectsArray()
    {
        var (source, handler) = Make(Payload, portFile: null);

        var projects = await source.FetchAsync(CancellationToken.None);

        projects.Count.ShouldBe(2);
        projects[0].Id.ShouldBe("7450aeab");
        projects[0].Name.ShouldBe("Grimoire");
        projects[0].Description.ShouldBe("memory");
        projects[0].DiskPath.ShouldBe("/mnt/Projects/Grimoire");
        projects[1].DiskPath.ShouldBeNull();
        projects[1].Description.ShouldBeNull();
        handler.Requests.ShouldHaveSingleItem().RequestUri!.ToString().ShouldBe("http://127.0.0.1:4761/api/projects");
    }

    [Fact]
    public async Task Fetch_PortFile_OverridesTheFallbackPort()
    {
        var directory = Directory.CreateTempSubdirectory("grimoire-board-tests");
        try
        {
            var portFile = Path.Combine(directory.FullName, "host.port");
            await File.WriteAllTextAsync(portFile, "5177\n");
            var (source, handler) = Make(Payload, portFile);

            await source.FetchAsync(CancellationToken.None);

            handler.Requests.ShouldHaveSingleItem().RequestUri!.ToString().ShouldBe("http://127.0.0.1:5177/api/projects");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Fetch_GarbagePortFile_FallsBackToTheDefaultPort()
    {
        var directory = Directory.CreateTempSubdirectory("grimoire-board-tests");
        try
        {
            var portFile = Path.Combine(directory.FullName, "host.port");
            await File.WriteAllTextAsync(portFile, "not-a-port");
            var (source, handler) = Make(Payload, portFile);

            await source.FetchAsync(CancellationToken.None);

            handler.Requests.ShouldHaveSingleItem().RequestUri!.ToString().ShouldBe("http://127.0.0.1:4761/api/projects");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Fetch_HostDown_WrapsAsUpstreamUnavailable()
    {
        var (source, _) = Make("unused", portFile: null, handler: _ => throw new HttpRequestException("refused"));

        var exception = await Should.ThrowAsync<UpstreamUnavailableException>(
            () => source.FetchAsync(CancellationToken.None));

        exception.Upstream.ShouldBe("overmind");
    }

    [Fact]
    public async Task Fetch_NonJsonPayload_ThrowsBoardResponseException()
    {
        var (source, _) = Make("<html>nope</html>", portFile: null);

        await Should.ThrowAsync<BoardResponseException>(() => source.FetchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_NonArrayPayload_ThrowsBoardResponseException()
    {
        var (source, _) = Make("""{ "projects": [] }""", portFile: null);

        await Should.ThrowAsync<BoardResponseException>(() => source.FetchAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Fetch_EntryWithoutIdOrName_ThrowsBoardResponseException()
    {
        var (source, _) = Make("""[ { "id": "x", "name": "" } ]""", portFile: null);

        await Should.ThrowAsync<BoardResponseException>(() => source.FetchAsync(CancellationToken.None));
    }

    private static (OvermindBoardProjectSource Source, FakeOllamaHandler Handler) Make(
        string payload,
        string? portFile,
        Func<HttpRequestMessage, HttpResponseMessage>? handler = null)
    {
        FakeOllamaHandler messageHandler = handler is null
            ? new FakeOllamaHandler(_ => FakeOllamaHandler.Json(payload))
            : new FakeOllamaHandler(handler);
        return (
            new OvermindBoardProjectSource(new HttpClient(messageHandler), portFile),
            messageHandler);
    }
}
