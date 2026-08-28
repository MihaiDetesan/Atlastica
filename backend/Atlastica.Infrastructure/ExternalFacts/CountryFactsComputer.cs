using Atlastica.Nlp;
using Microsoft.Extensions.Logging;

namespace Atlastica.Infrastructure.ExternalFacts;

public record ComputedCountryFacts(
    string IsoCode,
    long? Population,
    string? Capital,
    string? GovernmentForm,
    List<string> OfficialLanguages,
    double? AreaKm2,
    string? Currency,
    string? NotableGeographicFeatures,
    string? BiodiversityFacts,
    DateTimeOffset FetchedAtUtc);

/// <summary>
/// Combines Wikidata (structured facts) + Wikipedia (raw text) + local NLP
/// extraction into one country's facts. This is the expensive, offline part
/// of the pipeline - used only by Atlastica.FactsExtractor, never on the API
/// request path (see CountryFactsService, which just reads what this already
/// wrote to Postgres).
/// </summary>
public class CountryFactsComputer(
    WikidataCountryFetcher wikidataFetcher,
    WikipediaFetcher wikipediaFetcher,
    IFactExtractionService extractionService,
    ILogger<CountryFactsComputer> logger)
{
    public async Task<ComputedCountryFacts> ComputeAsync(string isoCode, string countryName, CancellationToken ct = default)
    {
        logger.LogInformation("Computing facts for {IsoCode} ({Name})", isoCode, countryName);

        var wikidataFacts = await wikidataFetcher.FetchAsync(isoCode, ct);
        var article = await wikipediaFetcher.FetchAsync(countryName, ct);
        var extraction = article is not null
            ? await extractionService.ExtractCountryFactsAsync(countryName, article.PlainText, ct)
            : new CountryFactExtractionResult(null, null, null);

        return new ComputedCountryFacts(
            isoCode,
            wikidataFacts?.Population,
            wikidataFacts?.Capital,
            // Wikidata's structured "basic form of government" is more
            // reliable than the model's free-text summary - prefer it, fall
            // back to the NLP extraction only when Wikidata has no value.
            wikidataFacts?.GovernmentForm ?? extraction.GovernmentForm,
            wikidataFacts?.OfficialLanguages ?? [],
            wikidataFacts?.AreaKm2,
            wikidataFacts?.Currency,
            extraction.NotableGeographicFeatures,
            extraction.BiodiversityFacts,
            DateTimeOffset.UtcNow);
    }
}
