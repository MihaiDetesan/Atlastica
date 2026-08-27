using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Atlastica.Infrastructure.ExternalFacts;

public record WikidataCountryFacts(long? Population, double? AreaKm2, string? Currency, List<string> OfficialLanguages, string? GovernmentForm);

/// <summary>
/// Fetches structured country facts from Wikidata's public SPARQL endpoint -
/// free, keyless, and already precedented in Historica's pipeline
/// (pipeline/fetch_wikidata.py). Chosen over the REST Countries API, whose
/// free v3.1 endpoint was retired in favor of a paid, API-key-gated v5.
/// </summary>
public class WikidataCountryFetcher(HttpClient httpClient)
{
    private const string Endpoint = "https://query.wikidata.org/sparql";

    // P297 = ISO 3166-1 alpha-2 code, P1082 = population, P2046 = area (km2),
    // P38 = currency, P37 = official language, P122 = basic form of government.
    private const string QueryTemplate = """
        SELECT (SAMPLE(?population) as ?population) (SAMPLE(?area) as ?area)
               (SAMPLE(?currencyLabel) as ?currencyLabel) (SAMPLE(?govLabel) as ?govLabel)
               (GROUP_CONCAT(DISTINCT ?langLabel; separator="|") as ?languages)
        WHERE {{
          ?country wdt:P297 "{0}".
          OPTIONAL {{ ?country wdt:P1082 ?population. }}
          OPTIONAL {{ ?country wdt:P2046 ?area. }}
          OPTIONAL {{ ?country wdt:P38 ?currency. ?currency rdfs:label ?currencyLabel. FILTER(LANG(?currencyLabel)="en") }}
          OPTIONAL {{ ?country wdt:P37 ?lang. ?lang rdfs:label ?langLabel. FILTER(LANG(?langLabel)="en") }}
          OPTIONAL {{ ?country wdt:P122 ?gov. ?gov rdfs:label ?govLabel. FILTER(LANG(?govLabel)="en") }}
        }}
        """;

    private record SparqlResponse([property: JsonPropertyName("results")] SparqlResults Results);
    private record SparqlResults([property: JsonPropertyName("bindings")] List<Dictionary<string, SparqlValue>> Bindings);
    private record SparqlValue([property: JsonPropertyName("value")] string Value);

    public async Task<WikidataCountryFacts?> FetchAsync(string isoAlpha2Code, CancellationToken ct = default)
    {
        var query = string.Format(QueryTemplate, isoAlpha2Code);
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
        if (!response.IsSuccessStatusCode) return null;

        var data = await response.Content.ReadFromJsonAsync<SparqlResponse>(cancellationToken: ct);
        var binding = data?.Results.Bindings.FirstOrDefault();
        if (binding is null) return null;

        var population = binding.TryGetValue("population", out var pop) && long.TryParse(pop.Value, out var p) ? p : (long?)null;
        var area = binding.TryGetValue("area", out var ar) && double.TryParse(ar.Value, out var a) ? a : (double?)null;
        var currency = binding.TryGetValue("currencyLabel", out var cur) ? cur.Value : null;
        var government = binding.TryGetValue("govLabel", out var gov) ? gov.Value : null;
        var languages = binding.TryGetValue("languages", out var langs) && !string.IsNullOrEmpty(langs.Value)
            ? langs.Value.Split('|').ToList()
            : [];

        return new WikidataCountryFacts(population, area, currency, languages, government);
    }
}
