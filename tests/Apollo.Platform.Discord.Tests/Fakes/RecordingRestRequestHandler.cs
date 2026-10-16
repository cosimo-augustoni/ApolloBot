using System.Net;
using System.Text;
using NetCord.Rest;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// NetCord's HTTP seam: records every REST request and answers 204 No Content, so no request reaches Discord. Calls
/// whose result NetCord parses get a JSON body from <see cref="Respond"/>.
/// </summary>
internal sealed class RecordingRestRequestHandler : IRestRequestHandler
{
    private readonly List<(HttpMethod Method, string Path)> _requests = [];
    private readonly List<string?> _bodies = [];

    public IReadOnlyList<(HttpMethod Method, string Path)> Requests => _requests;

    /// <summary>
    /// The request bodies, in the order of <see cref="Requests"/>; <see langword="null"/> for requests without one.
    /// </summary>
    public IReadOnlyList<string?> Bodies => _bodies;

    /// <summary>
    /// The JSON to answer a request with (200 OK); <see langword="null"/> answers 204 No Content.
    /// </summary>
    public Func<HttpRequestMessage, string?> Respond { get; set; } = _ => null;

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Add((request.Method, request.RequestUri!.AbsolutePath));
        _bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        return Respond(request) is { } json
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }
            : new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    public void AddDefaultHeader(string name, IEnumerable<string> values)
    {
    }

    public void Dispose()
    {
    }
}
