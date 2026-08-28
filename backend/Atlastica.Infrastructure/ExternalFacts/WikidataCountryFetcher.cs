using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Atlastica.Infrastructure.ExternalFacts;

public record WikidataCountryFacts(long? Population, double? AreaKm2, string? Currency, List<string> OfficialLanguages, string? GovernmentForm, string? Capital);

/// <summary>
/// Fetches structured country facts from Wikidata's public SPARQL endpoint -
/// free, keyless, and already precedented in Historica's pipeline
/// (pipeline/fetch_wikidata.py). Chosen over the REST Countries API, whose
/// free v3.1 endpoint was retired in favor of a paid, API-key-gated v5.
/// </summary>
public class WikidataCountryFetcher(HttpClient httpClient, ILogger<WikidataCountryFetcher> logger)
{
    private const string Endpoint = "https://query.wikidata.org/sparql";

    // query.wikidata.org is a shared public endpoint that occasionally
    // returns 502/504 or times out under load (Historica's own SPARQL
    // fetcher retries for the same reason) - retry with backoff instead of
    // silently leaving a country's facts null because of a blip.
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    // P297 = ISO 3166-1 alpha-2 code, P1082 = population, P2046 = area (km2),
    // P38 = currency, P37 = official language, P122 = basic form of government,
    // P36 = capital.
    private const string QueryTemplate = """
        SELECT (SAMPLE(?population) as ?population) (SAMPLE(?area) as ?area)
               (SAMPLE(?currencyLabel) as ?currencyLabel) (SAMPLE(?govLabel) as ?govLabel)
               (SAMPLE(?capitalLabel) as ?capitalLabel)
               (GROUP_CONCAT(DISTINCT ?langLabel; separator="|") as ?languages)
        WHERE {{
          ?country wdt:P297 "{0}".
          OPTIONAL {{ ?country wdt:P1082 ?population. }}
          OPTIONAL {{ ?country wdt:P2046 ?area. }}
          OPTIONAL {{ ?country wdt:P38 ?currency. ?currency rdfs:label ?currencyLabel. FILTER(LANG(?currencyLabel)="en") }}
          OPTIONAL {{ ?country wdt:P37 ?lang. ?lang rdfs:label ?langLabel. FILTER(LANG(?langLabel)="en") }}
          OPTIONAL {{ ?country wdt:P122 ?gov. ?gov rdfs:label ?govLabel. FILTER(LANG(?govLabel)="en") }}
          OPTIONAL {{ ?country wdt:P36 ?capital. ?capital rdfs:label ?capitalLabel. FILTER(LANG(?capitalLabel)="en") }}
        }}
        """;

    private record SparqlResponse([property: JsonPropertyName("results")] SparqlResults Results);
    private record SparqlResults([property: JsonPropertyName("bindings")] List<Dictionary<string, SparqlValue>> Bindings);
    private record SparqlValue([property: JsonPropertyName("value")] string Value);

    public async Task<WikidataCountryFacts?> FetchAsync(string isoAlpha2Code, CancellationToken ct = default)
    {
        var data = await SendWithRetryAsync(isoAlpha2Code, ct);
        var binding = data?.Results.Bindings.FirstOrDefault();
        if (binding is null) return null;

        var population = binding.TryGetValue("population", out var pop) && long.TryParse(pop.Value, out var p) ? p : (long?)null;
        var area = binding.TryGetValue("area", out var ar) && double.TryParse(ar.Value, out var a) ? a : (double?)null;
        var currency = binding.TryGetValue("currencyLabel", out var cur) ? cur.Value : null;
        var government = binding.TryGetValue("govLabel", out var gov) ? gov.Value : null;
        var capital = binding.TryGetValue("capitalLabel", out var cap) ? cap.Value : null;
        var languages = binding.TryGetValue("languages", out var langs) && !string.IsNullOrEmpty(langs.Value)
            ? langs.Value.Split('|').ToList()
            : [];

        return new WikidataCountryFacts(population, area, currency, languages, government, capital);
    }

    private async Task<SparqlResponse?> SendWithRetryAsync(string isoAlpha2Code, CancellationToken ct)
    {
        var query = string.Format(QueryTemplate, isoAlpha2Code);

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["query"] = query,
                        ["format"] = "json",
                    }),
                };
                request.Headers.Accept.ParseAdd("application/sparql-results+json");

                using var response = await httpClient.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<SparqlResponse>(cancellationToken: ct);
                }

                logger.LogWarning(
                    "Wikidata query for {IsoCode} returned {StatusCode} (attempt {Attempt}/{MaxRetries})",
                    isoAlpha2Code, (int)response.StatusCode, attempt, MaxRetries);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < MaxRetries)
            {
                logger.LogWarning(ex, "Wikidata query for {IsoCode} failed (attempt {Attempt}/{MaxRetries})", isoAlpha2Code, attempt, MaxRetries);
            }

            if (attempt < MaxRetries) await Task.Delay(RetryDelay, ct);
        }

        return null;
    }
}
