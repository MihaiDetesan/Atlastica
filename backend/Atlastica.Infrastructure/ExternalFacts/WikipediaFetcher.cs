using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Atlastica.Infrastructure.ExternalFacts;

public record WikipediaArticle(string Title, string PlainText, string? ImageUrl, string PageUrl);

/// <summary>
/// Fetches raw article text + lead image from Wikipedia/Wikimedia. The raw
/// text is handed to Atlastica.Nlp for structured-field extraction - this
/// class only deals with retrieval, not parsing.
/// </summary>
public class WikipediaFetcher(HttpClient httpClient, ILogger<WikipediaFetcher> logger)
{
    // Article text is capped to keep the local model's prompt short (it only
    // needs the intro + geography/government sections, not the full article).
    private const int MaxPlainTextLength = 8000;

    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private record SummaryImage([property: JsonPropertyName("source")] string Source);

    private record SummaryResponse(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("content_urls")] ContentUrls? ContentUrls,
        [property: JsonPropertyName("originalimage")] SummaryImage? OriginalImage,
        [property: JsonPropertyName("thumbnail")] SummaryImage? Thumbnail);

    private record ContentUrls([property: JsonPropertyName("desktop")] ContentUrlDesktop? Desktop);
    private record ContentUrlDesktop([property: JsonPropertyName("page")] string Page);

    private record ExtractsQueryResponse([property: JsonPropertyName("query")] ExtractsQuery Query);
    private record ExtractsQuery([property: JsonPropertyName("pages")] Dictionary<string, ExtractsPage> Pages);
    private record ExtractsPage([property: JsonPropertyName("extract")] string? Extract);

    public async Task<WikipediaArticle?> FetchAsync(string title, CancellationToken ct = default)
    {
        var summary = await FetchSummaryAsync(title, ct);
        if (summary is null) return null;

        var plainText = await FetchFullExtractAsync(title, ct) ?? string.Empty;
        if (plainText.Length > MaxPlainTextLength) plainText = plainText[..MaxPlainTextLength];

        var imageUrl = summary.OriginalImage?.Source ?? summary.Thumbnail?.Source;
        var pageUrl = summary.ContentUrls?.Desktop?.Page ?? $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}";

        return new WikipediaArticle(summary.Title, plainText, imageUrl, pageUrl);
    }

    private Task<SummaryResponse?> FetchSummaryAsync(string title, CancellationToken ct) =>
        GetWithRetryAsync<SummaryResponse>(
            $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}", ct);

    private async Task<string?> FetchFullExtractAsync(string title, CancellationToken ct)
    {
        var url = "https://en.wikipedia.org/w/api.php" +
            "?action=query&prop=extracts&explaintext=1&exsectionformat=plain&format=json" +
            $"&titles={Uri.EscapeDataString(title)}";
        var data = await GetWithRetryAsync<ExtractsQueryResponse>(url, ct);
        return data?.Query.Pages.Values.FirstOrDefault()?.Extract;
    }

    private async Task<T?> GetWithRetryAsync<T>(string url, CancellationToken ct) where T : class
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                }

                logger.LogWarning("GET {Url} returned {StatusCode} (attempt {Attempt}/{MaxRetries})", url, (int)response.StatusCode, attempt, MaxRetries);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < MaxRetries)
            {
                logger.LogWarning(ex, "GET {Url} failed (attempt {Attempt}/{MaxRetries})", url, attempt, MaxRetries);
            }

            if (attempt < MaxRetries) await Task.Delay(RetryDelay, ct);
        }

        return null;
    }
}
