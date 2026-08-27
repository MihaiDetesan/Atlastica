using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Atlastica.Infrastructure.ExternalFacts;

public record WikipediaArticle(string Title, string PlainText, string? ImageUrl, string PageUrl);

/// <summary>
/// Fetches raw article text + lead image from Wikipedia/Wikimedia. The raw
/// text is handed to Atlastica.Nlp for structured-field extraction - this
/// class only deals with retrieval, not parsing.
/// </summary>
public class WikipediaFetcher(HttpClient httpClient)
{
    // Article text is capped to keep the local model's prompt short (it only
    // needs the intro + geography/government sections, not the full article).
    private const int MaxPlainTextLength = 8000;

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

    private async Task<SummaryResponse?> FetchSummaryAsync(string title, CancellationToken ct)
    {
        var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(title)}";
        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SummaryResponse>(cancellationToken: ct);
    }

    private async Task<string?> FetchFullExtractAsync(string title, CancellationToken ct)
    {
        var url = "https://en.wikipedia.org/w/api.php" +
            "?action=query&prop=extracts&explaintext=1&exsectionformat=plain&format=json" +
            $"&titles={Uri.EscapeDataString(title)}";
        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;

        var data = await response.Content.ReadFromJsonAsync<ExtractsQueryResponse>(cancellationToken: ct);
        return data?.Query.Pages.Values.FirstOrDefault()?.Extract;
    }
}
