using MediatR;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// A mediator for controller tests: records each request sent (and its cancellation token), and answers with what
/// <paramref name="respond"/> returns for it (the response type's default when it returns nothing that fits).
/// </summary>
public sealed class RecordingMediator(Func<object, object?>? respond = null) : IMediator
{
    public List<object> Sent { get; } = [];
    public List<CancellationToken> Tokens { get; } = [];

    /// <summary>The one request sent.</summary>
    public object Request => Sent.Single();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        Record(request, cancellationToken);
        return Task.FromResult(respond?.Invoke(request) is TResponse response ? response : default!);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        Record(request!, cancellationToken);
        return Task.CompletedTask;
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish(object notification, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification =>
        throw new NotSupportedException();

    private void Record(object request, CancellationToken cancellationToken)
    {
        Sent.Add(request);
        Tokens.Add(cancellationToken);
    }
}
