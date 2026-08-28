using Atlastica.Domain;
using Atlastica.Infrastructure.ExternalFacts;
using Atlastica.Nlp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlastica.Infrastructure;

public class FactsCacheOptions
{
    public int TtlDays { get; set; } = 50;
}

/// <summary>
/// Cache-or-refetch for per-country facts: serves straight from Postgres if
/// `fetched_at` is within the configured TTL, otherwise re-fetches from
/// Wikidata + Wikipedia, re-runs local NLP extraction, and updates the row.
/// </summary>
public class CountryFactsService(
    AtlasticaDbContext db,
    WikidataCountryFetcher wikidataFetcher,
    WikipediaFetcher wikipediaFetcher,
    IFactExtractionService extractionService,
    IOptions<FactsCacheOptions> cacheOptions,
    ILogger<CountryFactsService> logger)
{
    public async Task<CountryFacts?> GetFactsAsync(string isoCode, CancellationToken ct = default)
    {
        var country = await db.Countries.FindAsync([isoCode], ct);
        if (country is null) return null;

        var existing = await db.CountryFacts.FirstOrDefaultAsync(f => f.IsoCode == isoCode, ct);
        var ttl = TimeSpan.FromDays(cacheOptions.Value.TtlDays);
        if (existing is not null && DateTimeOffset.UtcNow - existing.FetchedAt < ttl)
        {
            return existing;
        }

        logger.LogInformation("Refreshing country facts for {IsoCode} ({Reason})", isoCode, existing is null ? "no cache" : "stale");

        var wikidataFacts = await wikidataFetcher.FetchAsync(isoCode, ct);
        var article = await wikipediaFetcher.FetchAsync(country.Name, ct);
        var extraction = article is not null
            ? await extractionService.ExtractCountryFactsAsync(country.Name, article.PlainText, ct)
            : new CountryFactExtractionResult(null, null, null, null);

        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            existing = new CountryFacts { IsoCode = isoCode, FetchedAt = now };
            db.CountryFacts.Add(existing);
        }

        existing.Population = wikidataFacts?.Population;
        existing.Capital = wikidataFacts?.Capital;
        existing.AreaKm2 = wikidataFacts?.AreaKm2;
        existing.Currency = wikidataFacts?.Currency;
        existing.OfficialLanguages = wikidataFacts?.OfficialLanguages ?? [];
        // Wikidata's structured "basic form of government" is more reliable
        // than the model's free-text summary - prefer it, fall back to the
        // NLP extraction only when Wikidata has no value.
        existing.GovernmentForm = wikidataFacts?.GovernmentForm ?? extraction.GovernmentForm;
        existing.MainMountains = extraction.MainMountains;
        existing.MainRivers = extraction.MainRivers;
        existing.BiodiversityFacts = extraction.BiodiversityFacts;
        existing.FetchedAt = now;

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
