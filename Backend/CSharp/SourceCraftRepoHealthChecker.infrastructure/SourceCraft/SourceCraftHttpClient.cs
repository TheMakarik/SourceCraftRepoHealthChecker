using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SourceCraftRepoHealthChecker.Domain.Enums;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class SourceCraftHttpClient(
    HttpClient httpClient,
    ILogger<SourceCraftHttpClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<SourceCraftPage<T>> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        GetAsync<T>(path, null, cancellationToken);

    public Task<SourceCraftPage<T>> GetAsync<T>(string path, string? accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrEmpty(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return SendAsync<T>(request, cancellationToken);
    }

    public Task<SourceCraftPage<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };

        return SendAsync<TResponse>(request, cancellationToken);
    }

    public Task<SourceCraftPage<TResponse>> PostAsync<TRequest, TResponse>(string path, TRequest body, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<SourceCraftPage<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? request.RequestUri?.ToString();
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("SourceCraft {Method} {Path} returned {StatusCode}", request.Method, path, (int)response.StatusCode);
            return new SourceCraftPage<T>(DataStatus.Unavailable, default, $"SourceCraft service returned {(int)response.StatusCode}", null);
        }

        var envelope = await response.Content.ReadFromJsonAsync<SourceCraftEnvelope<T>>(JsonOptions, cancellationToken);
        if (envelope is null)
        {
            logger.LogWarning("SourceCraft {Method} {Path} returned an empty response", request.Method, path);
            return new SourceCraftPage<T>(DataStatus.Unavailable, default, "SourceCraft service returned an empty response", null);
        }

        var status = ParseStatus(envelope.Status);
        logger.LogDebug("SourceCraft {Method} {Path} returned {Status}", request.Method, path, status);
        return new SourceCraftPage<T>(status, envelope.Data, envelope.Reason, envelope.NextPageToken);
    }

    private static DataStatus ParseStatus(string status) => status switch
    {
        "Available" => DataStatus.Available,
        "NoData" => DataStatus.NoData,
        _ => DataStatus.Unavailable
    };
}
