using System.Net;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Infrastructure.Services;
using LocalMediaTranslator.Tests.Helpers;

namespace LocalMediaTranslator.Tests;

public class GraphQlMediaClientTests {
    private readonly MediaClientOptions _defaultOptions = new() {
        Endpoint = new Uri("http://localhost:9999/graphql"),
        ApiKey = "test-api-key"
    };

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new GraphQlMediaClient(null!, _defaultOptions));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException() {
        var client = new HttpClient(new MockHttpMessageHandler("{}"));
        Assert.Throws<ArgumentNullException>(() => new GraphQlMediaClient(client, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSceneAsync_NullOrWhiteSpaceId_ThrowsArgumentException(string? invalidId) {
        var client = new GraphQlMediaClient(new HttpClient(new MockHttpMessageHandler("{}")), _defaultOptions);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.GetSceneAsync(invalidId!));
    }

    [Fact]
    public async Task GetSceneAsync_SceneNotFound_ReturnsNull() {
        var jsonResponse = """
            {
                "data": {
                    "findScene": null
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.GetSceneAsync("42");

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetSceneAsync_SceneExists_ReturnsMappedMediaScene() {
        var jsonResponse = """
            {
                "data": {
                    "findScene": {
                        "id": "100",
                        "title": "Sample Scene",
                        "files": [
                            {
                                "id": "f1",
                                "path": "/media/videos/sample.mp4"
                            }
                        ],
                        "tags": [
                            {
                                "id": "t1",
                                "name": "Japanese"
                            },
                            {
                                "id": "t2",
                                "name": "1080p"
                            }
                        ]
                    }
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.GetSceneAsync("100");

        Assert.NotNull(result);
        Assert.Equal("100", result.Id);
        Assert.Equal("Sample Scene", result.Title);
        Assert.Single(result.Files);
        Assert.Equal("f1", result.Files[0].Id);
        Assert.Equal("/media/videos/sample.mp4", result.Files[0].AbsolutePath);
        Assert.Equal(2, result.Tags.Count);
        Assert.Equal("Japanese", result.Tags[0]);
        Assert.Equal("1080p", result.Tags[1]);
    }

    [Fact]
    public async Task GetSceneAsync_NullTitle_ReturnsEmptyStringTitle() {
        var jsonResponse = """
            {
                "data": {
                    "findScene": {
                        "id": "101",
                        "title": null,
                        "files": [],
                        "tags": []
                    }
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.GetSceneAsync("101");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Title);
    }

    [Fact]
    public async Task FindScenesAsync_NullFilter_ThrowsArgumentNullException() {
        var client = new GraphQlMediaClient(new HttpClient(new MockHttpMessageHandler("{}")), _defaultOptions);
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.FindScenesAsync(null!));
    }

    [Fact]
    public async Task FindScenesAsync_ScenesFound_ReturnsMappedList() {
        var jsonResponse = """
            {
                "data": {
                    "findScenes": {
                        "count": 2,
                        "scenes": [
                            {
                                "id": "1",
                                "title": "Scene 1",
                                "files": [
                                    { "id": "f1", "path": "/path/1.mp4" }
                                ],
                                "tags": [
                                    { "id": "t1", "name": "TagA" }
                                ]
                            },
                            {
                                "id": "2",
                                "title": null,
                                "files": [],
                                "tags": []
                            }
                        ]
                    }
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.FindScenesAsync(new MediaSceneFilter());

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Scene 1", result[0].Title);
        Assert.Single(result[0].Files);
        Assert.Equal("TagA", result[0].Tags[0]);
        Assert.Equal("2", result[1].Id);
        Assert.Equal(string.Empty, result[1].Title);
    }

    [Fact]
    public async Task FindScenesAsync_NoScenesReturned_ReturnsEmptyList() {
        var jsonResponse = """
            {
                "data": {
                    "findScenes": {
                        "count": 0,
                        "scenes": []
                    }
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.FindScenesAsync(new MediaSceneFilter());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindScenesAsync_WithFilters_SendsExpectedVariables() {
        var jsonResponse = """
            {
                "data": {
                    "findScenes": {
                        "count": 0,
                        "scenes": []
                    }
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var filter = new MediaSceneFilter(
            TagIds: new[] { "10", "20" },
            SearchTerm: "Tokyo",
            HasCaption: false,
            Page: 2,
            PerPage: 50
        );

        await client.FindScenesAsync(filter);

        Assert.NotNull(handler.LastRequestBody);
        var requestBody = handler.LastRequestBody;

        Assert.Contains("\"page\":2", requestBody);
        Assert.Contains("\"per_page\":50", requestBody);
        Assert.Contains("\"q\":\"Tokyo\"", requestBody);
        Assert.Contains("\"captions\":{\"value\":\"\",\"modifier\":\"IS_NULL\"}", requestBody);
        Assert.Contains("\"value\":[\"10\",\"20\"]", requestBody);
    }

    [Fact]
    public async Task TriggerMetadataScanAsync_NullPaths_ThrowsArgumentNullException() {
        var client = new GraphQlMediaClient(new HttpClient(new MockHttpMessageHandler("{}")), _defaultOptions);
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.TriggerMetadataScanAsync(null!));
    }

    [Fact]
    public async Task TriggerMetadataScanAsync_Success_ReturnsTrue() {
        var jsonResponse = """
            {
                "data": {
                    "metadataScan": "job-12345"
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.TriggerMetadataScanAsync(new[] { "/media/videos/scene.mp4" });

        Assert.True(result);
        Assert.NotNull(handler.LastRequestBody);
        var requestBody = handler.LastRequestBody;
        Assert.Contains("/media/videos/scene.mp4", requestBody);
    }

    [Fact]
    public async Task TriggerMetadataScanAsync_NullResult_ReturnsFalse() {
        var jsonResponse = """
            {
                "data": {
                    "metadataScan": null
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var result = await client.TriggerMetadataScanAsync(new[] { "/media/videos/scene.mp4" });

        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteQueryAsync_GraphQLError_ThrowsInvalidOperationException() {
        var jsonResponse = """
            {
                "errors": [
                    {
                        "message": "Unauthorized access to GraphQL Media Server API"
                    }
                ]
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSceneAsync("1"));
        Assert.Contains("Unauthorized access to GraphQL Media Server API", ex.Message);
    }

    [Fact]
    public async Task ExecuteQueryAsync_ApiKeyPresent_AddsApiKeyHeader() {
        var jsonResponse = """
            {
                "data": {
                    "findScene": null
                }
            }
            """;

        var handler = new MockHttpMessageHandler(jsonResponse);
        var options = new MediaClientOptions {
            Endpoint = new Uri("http://localhost:9999/graphql"),
            ApiKey = "secret-token-xyz"
        };
        var client = new GraphQlMediaClient(new HttpClient(handler), options);

        await client.GetSceneAsync("1");

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest.Headers.Contains("ApiKey"));
        Assert.Equal("secret-token-xyz", handler.LastRequest.Headers.GetValues("ApiKey").First());
    }

    [Fact]
    public async Task ExecuteQueryAsync_HttpErrorStatus_ThrowsHttpRequestException() {
        var handler = new MockHttpMessageHandler("{}", HttpStatusCode.InternalServerError);
        var client = new GraphQlMediaClient(new HttpClient(handler), _defaultOptions);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetSceneAsync("1"));
    }
}
