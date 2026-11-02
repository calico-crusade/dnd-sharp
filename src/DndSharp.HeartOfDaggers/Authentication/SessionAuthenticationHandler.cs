using System.Net;

namespace DndSharp.HeartOfDaggers.Authentication;

/// <summary>Obtains a session before requests and retries safe reads after session expiry.</summary>
public sealed class SessionAuthenticationHandler(HeartSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await session.EnsureAsync(cancellationToken);
        var generation = session.Generation;
        var response = await base.SendAsync(request, cancellationToken);
        if (request.Method != HttpMethod.Get || response.StatusCode is not
            (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Redirect)) return response;

        // A permissions error must not trigger another login if the session itself is still valid.
        if (!await session.RefreshIfExpiredAsync(generation, cancellationToken)) return response;
        response.Dispose();
        using var retry = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers)
            retry.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return await base.SendAsync(retry, cancellationToken);
    }
}
