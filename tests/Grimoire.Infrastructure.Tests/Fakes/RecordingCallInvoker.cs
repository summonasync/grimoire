using Grpc.Core;
using Qdrant.Client.Grpc;

namespace Grimoire.Infrastructure.Tests.Fakes;

public sealed class RecordingCallInvoker : CallInvoker
{
    public List<object> UnaryRequests { get; } = [];

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException($"Unexpected blocking unary call '{method.FullName}'.");

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        UnaryRequests.Add(request);

        if (typeof(TResponse) != typeof(CollectionOperationResponse))
        {
            throw new NotSupportedException($"Unexpected unary call '{method.FullName}'.");
        }

        return new AsyncUnaryCall<TResponse>(
            Task.FromResult((TResponse)(object)new CollectionOperationResponse { Result = true }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) =>
        throw new NotSupportedException($"Unexpected client streaming call '{method.FullName}'.");

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException($"Unexpected server streaming call '{method.FullName}'.");

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) =>
        throw new NotSupportedException($"Unexpected duplex streaming call '{method.FullName}'.");
}
