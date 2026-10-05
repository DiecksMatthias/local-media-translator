using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LocalMediaTranslator.Tests.Helpers;

public class MockHttpMessageHandler : HttpMessageHandler {
    private readonly Func<HttpRequestMessage, HttpResponseMessage>? _handlerFunc;
    private readonly string _responseJson;
    private readonly HttpStatusCode _statusCode;

    public int CallCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpRequestHeaders? LastRequestHeaders { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = new();

    public MockHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK) {
        _responseJson = responseJson;
        _statusCode = statusCode;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) {
        _handlerFunc = handlerFunc;
        _responseJson = string.Empty;
        _statusCode = HttpStatusCode.OK;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        CallCount++;
        LastRequest = request;
        Requests.Add(request);
        LastRequestHeaders = request.Headers;
        LastRequestBody = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : null;

        if (_handlerFunc != null) {
            return _handlerFunc(request);
        }

        var response = new HttpResponseMessage(_statusCode) {
            Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
        };
        return response;
    }
}
