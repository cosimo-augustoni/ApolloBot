using System.Net;
using System.Net.Http.Json;

namespace Apollo.Web.Client.Tests.Api;

/// <summary>Answers every request with the given JSON body and records the requested URIs.</summary>
public sealed class StubHttpMessageHandler(object responseBody) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public HttpClient CreateClient() => new(this) { BaseAddress = new Uri("http://apollo.test/") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(responseBody) });
    }
}
