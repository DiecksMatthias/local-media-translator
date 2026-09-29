using System.Net.Http.Json;
using System.Text.Json;
using LocalMediaTranslator.Core.Interfaces;
using LocalMediaTranslator.Core.Models;
using LocalMediaTranslator.Core.Utilities;

namespace LocalMediaTranslator.Infrastructure.Services;

public class GraphQlMediaClient : IMediaServerClient {
    private readonly HttpClient _httpClient;
    private readonly MediaClientOptions _options;

    public GraphQlMediaClient(HttpClient httpClient, MediaClientOptions options) {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
    public async Task<IReadOnlyList<MediaScene>> FindScenesAsync(MediaSceneFilter filter, CancellationToken cs = default) {
        // Guards
        ArgumentNullException.ThrowIfNull(filter);

        const string query = """
                        query FindScenes($filter: FindFilterType, $scene_filter: SceneFilterType) {
                            findScenes(filter: $filter, scene_filter: $scene_filter) {
                                count
                                scenes {
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
                        }
                        """;

        // building variables && execute graphql call
        var findFilterType = new {
            page = filter.Page,
            per_page = filter.PerPage,
            q = string.IsNullOrWhiteSpace(filter.SearchTerm) ? null : filter.SearchTerm
        };
        var sceneFilterType = new {
            tags = filter.TagIds?.Count > 0 ? new { value = filter.TagIds, modifier = "INCLUDES" } : null,
            captions = filter.HasCaption.HasValue ? new { value = "", modifier = filter.HasCaption.Value ? "NOT_NULL" : "IS_NULL" } : null
        };
        var variables = new {
            filter = findFilterType,
            scene_filter = sceneFilterType
        };
        var data = await ExecuteQueryAsync(query, variables, cs);

        List<MediaScene> scenes = new();
        if (data.TryGetProperty("findScenes", out var findScenesElement) && findScenesElement.ValueKind != JsonValueKind.Null) {
            var sceneElements = findScenesElement.GetProperty("scenes");
            foreach (var sceneElement in sceneElements.EnumerateArray()) {
                var id = sceneElement.GetProperty("id").GetString()!;
                var title = sceneElement.GetProperty("title").GetString() ?? string.Empty;
                var files = sceneElement.GetProperty("files").EnumerateArray()
                    .Select(f => new MediaFile(
                        f.GetProperty("id").GetString()!,
                        PathTransformer.TransformPath(f.GetProperty("path").GetString()!, _options.PathMappings),
                        null, null, null)).ToList();
                var tags = sceneElement.GetProperty("tags").EnumerateArray()
                    .Select(t => t.GetProperty("name").GetString()!)
                    .ToList();
                scenes.Add(new MediaScene(id, title, files, tags));
            }
            return scenes;
        }
        return scenes;
    }

    public async Task<MediaScene?> GetSceneAsync(string sceneId, CancellationToken cs = default) {
        //Guards
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
                    PathTransformer.TransformPath(f.GetProperty("path").GetString()!, _options.PathMappings),
                    null, null, null
                )).ToList();

            var tags = sceneElement.GetProperty("tags").EnumerateArray()
                .Select(t => t.GetProperty("name").GetString()!)
                .ToList();
            return new MediaScene(id, title, files, tags);
        }
        return null;
    }

    public async Task<bool> TriggerMetadataScanAsync(IReadOnlyList<string> paths, CancellationToken cs = default) {
        // Guards
        ArgumentNullException.ThrowIfNull(paths);

        var mutation = """
                mutation MetadataScan($input: ScanMetadataInput!) {
                    metadataScan(input: $input)
                }
            """;
        var variables = new {
            input = new {
                paths = paths
            }
        };
        var data = await ExecuteQueryAsync(mutation, variables, cs);

        if (data.TryGetProperty("metadataScan", out var metadataElement) && metadataElement.ValueKind != JsonValueKind.Null)
            return true;
        return false;
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
