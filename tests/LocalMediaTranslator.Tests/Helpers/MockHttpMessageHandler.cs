using System.Net;
using System.Text;

namespace LocalMediaTranslator.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler {
    private readonly string _responseJson;
    private readonly HttpStatusCode _statusCode;

    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public MockHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK) {
        _responseJson = responseJson;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        CallCount++;
        LastRequest = request;
        var response = new HttpResponseMessage(_statusCode) {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
