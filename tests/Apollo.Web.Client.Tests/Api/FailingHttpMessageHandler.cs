namespace Apollo.Web.Client.Tests.Api;

/// <summary>Fails every request as if the web host were unreachable.</summary>
public sealed class FailingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new HttpRequestException("The API is not reachable.");
}
