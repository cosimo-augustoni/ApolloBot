using System.Net;
using NetCord.Rest;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// NetCord's HTTP seam: records every REST request and answers 204 No Content, so no request reaches Discord.
/// </summary>
internal sealed class RecordingRestRequestHandler : IRestRequestHandler
{
    private readonly List<(HttpMethod Method, string Path)> _requests = [];

    public IReadOnlyList<(HttpMethod Method, string Path)> Requests => _requests;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add((request.Method, request.RequestUri!.AbsolutePath));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values)
    {
    }

    public void Dispose()
    {
    }
}
