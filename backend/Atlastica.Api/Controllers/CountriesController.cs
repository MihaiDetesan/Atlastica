using System.Text.Json;
using System.Text.RegularExpressions;
using Atlastica.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Atlastica.Api.Controllers;

public record CountryFeatureDto(string IsoCode, string Name, JsonElement Geometry);

public record CountryFactsDto(
    string IsoCode,
    long? Population,
    string? Capital,
    string? GovernmentForm,
    List<string> OfficialLanguages,
    double? AreaKm2,
    string? Currency,
    string? NotableGeographicFeatures,
    string? BiodiversityFacts,
    DateTimeOffset FetchedAt);

[ApiController]
[Route("api/countries")]
public class CountriesController(AtlasticaDbContext db, CountryFactsService factsService) : ControllerBase
{
    // ISO 3166-1 alpha-2. Enforced here because it flows unparameterized into
    // WikidataCountryFetcher's SPARQL query template - reject anything that
    // isn't a two-letter code before it can reach that string.Format call.
    private static readonly Regex IsoCodePattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CountryFeatureDto>>> GetAll(CancellationToken ct)
    {
        var countries = await db.Countries.AsNoTracking().ToListAsync(ct);

        var result = countries.Select(c => new CountryFeatureDto(
            c.IsoCode,
            c.Name,
            JsonDocument.Parse(c.BoundaryGeoJson).RootElement.Clone()));

        return Ok(result);
    }

    [HttpGet("{isoCode}/facts")]
    [EnableRateLimiting("facts")]
    public async Task<ActionResult<CountryFactsDto>> GetFacts(string isoCode, CancellationToken ct)
    {
        isoCode = isoCode.ToUpperInvariant();
        if (!IsoCodePattern.IsMatch(isoCode)) return NotFound();

        var facts = await factsService.GetFactsAsync(isoCode, ct);
        if (facts is null) return NotFound();

        return Ok(new CountryFactsDto(
            facts.IsoCode,
            facts.Population,
            facts.Capital,
            facts.GovernmentForm,
            facts.OfficialLanguages,
            facts.AreaKm2,
            facts.Currency,
            facts.NotableGeographicFeatures,
            facts.BiodiversityFacts,
            facts.FetchedAt));
    }
}
