using System.Net;
using Grimoire.Api.Contracts;
using Microsoft.AspNetCore.Http;

namespace Grimoire.Api.Endpoints;

internal static class ApiResponses
{
    public static IResult Error(HttpStatusCode statusCode, string code, string message) =>
        Results.Json(Payload(code, message), statusCode: (int)statusCode);

    public static object Payload(string code, string message) => new { error = new ApiError(code, message) };
}
