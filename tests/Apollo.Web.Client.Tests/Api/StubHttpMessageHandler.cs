using System.Net;
using System.Net.Http.Json;

namespace Apollo.Web.Client.Tests.Api;

/// <summary>
/// Answers every request with the given JSON body, unless <see cref="Respond"/> set another answer for its path, and
/// records the requests.
/// </summary>
public sealed class StubHttpMessageHandler(object responseBody) : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, object? Body)> _responses = [];

    public List<Uri> Requests { get; } = [];

    /// <summary>The request bodies, in order; read when sent, since the caller may dispose the request.</summary>
    public List<string?> Bodies { get; } = [];

    /// <summary>The requests with method and headers, in order.</summary>
    public List<HttpRequestMessage> Messages { get; } = [];

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://apollo.test/") };

    /// <summary>Answers requests to <paramref name="path"/> (e.g. <c>/auth/me</c>) with this status and JSON body.</summary>
    public StubHttpMessageHandler Respond(string path, HttpStatusCode status, object? body = null)
    {
        _responses[path] = (status, body);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        Messages.Add(request);
        Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));

        var (status, body) = _responses.TryGetValue(request.RequestUri!.AbsolutePath, out var response)
            ? response
            : (HttpStatusCode.OK, responseBody);
        return new HttpResponseMessage(status) { Content = body is null ? null : JsonContent.Create(body) };
    }
}
