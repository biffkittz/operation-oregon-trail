using System.Net;

namespace Oregon.Worker.Tests.Support;

/// <summary>Captures requests and returns a canned response.</summary>
internal sealed class StubHttpHandler(HttpStatusCode status, string body, TimeSpan? retryAfter = null) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        HttpResponseMessage response = new(status) { Content = new StringContent(body) };
        if (retryAfter is { } delay)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delay);
        }

        return response;
    }
}
