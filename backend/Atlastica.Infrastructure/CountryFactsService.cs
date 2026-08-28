using Atlastica.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atlastica.Infrastructure;

/// <summary>
/// Read-only lookup for per-country facts. Population happens entirely
/// offline (see Atlastica.FactsExtractor + CountryFactsComputer) - the API
/// never fetches from Wikidata/Wikipedia or runs NLP on the request path, it
/// only serves whatever's already in Postgres.
/// </summary>
public class CountryFactsService(AtlasticaDbContext db)
{
    public Task<CountryFacts?> GetFactsAsync(string isoCode, CancellationToken ct = default) =>
        db.CountryFacts.AsNoTracking().FirstOrDefaultAsync(f => f.IsoCode == isoCode, ct);
}
