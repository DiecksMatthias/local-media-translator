using System.Net.Http.Json;
using System.Text.Json;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;

namespace LocalMediaTranslator.Infrastructure.Services;

public class GraphQlMediaClient : IMediaServerClient {
    private readonly HttpClient _httpClient;
    private readonly MediaClientOptions _options;

    public GraphQlMediaClient(HttpClient httpClient, MediaClientOptions options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    public Task<IReadOnlyList<MediaScene>> FindScenesAsync(MediaSceneFilter filter, CancellationToken cs = default) {
        throw new NotImplementedException();
    }

    public async Task<MediaScene?> GetSceneAsync(string sceneId, CancellationToken cs = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneId);

        const string query = """
                        query FindScene($id: ID!) {
                        findScene(id: $id) {
                            id
                            title
                            files {
                                id
                                path
                            }
                            tags {
                                id
                                name
                            }
                        }
                    }
                    """;
        var variables = new { id = sceneId };
        JsonElement data = await ExecuteQueryAsync(query, variables, cs);

        if (data.TryGetProperty("findScene", out var sceneElement) && sceneElement.ValueKind != JsonValueKind.Null) {
            var id = sceneElement.GetProperty("id").GetString()!;
            var title = sceneElement.GetProperty("title").GetString() ?? string.Empty;

            var files = sceneElement.GetProperty("files").EnumerateArray()
                .Select(f => new MediaFile(
                    f.GetProperty("id").GetString()!,
                    f.GetProperty("path").GetString()!,
                    null, null, null
                )).ToList();

            var tags = sceneElement.GetProperty("tags").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()!)
                .ToList();
            return new MediaScene(id, title, files, tags);
        }
        return null;
    }

    public Task<bool> TriggerMetadataScanAsync(IReadOnlyList<string> paths, CancellationToken cs = default) {
        throw new NotImplementedException();
    }

    private async Task<JsonElement> ExecuteQueryAsync(string query, object? variables, CancellationToken cs) {
        var requestBody = new {
            query = query,
            variables = variables
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint) {
            Content = JsonContent.Create(requestBody)
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.Add("ApiKey", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cs);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cs));
        var root = doc.RootElement;

        // error checking and handling
        if (root.TryGetProperty("errors", out var errorsElement)) {
            var errorMessage = errorsElement[0].GetProperty("message").GetString();
            throw new InvalidOperationException($"GraphQL Error: {errorMessage}");
        }
        return root.GetProperty("data").Clone();
    }
}
