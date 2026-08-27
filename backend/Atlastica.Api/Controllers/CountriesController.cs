using System.Text.Json;
using Atlastica.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlastica.Api.Controllers;

public record CountryFeatureDto(string IsoCode, string Name, JsonElement Geometry);

public record CountryFactsDto(
    string IsoCode,
    long? Population,
    string? GovernmentForm,
    List<string> OfficialLanguages,
    double? AreaKm2,
    string? Currency,
    string? MainMountains,
    string? MainRivers,
    string? BiodiversityFacts,
    DateTimeOffset FetchedAt);

[ApiController]
[Route("api/countries")]
public class CountriesController(AtlasticaDbContext db, CountryFactsService factsService) : ControllerBase
{
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
    public async Task<ActionResult<CountryFactsDto>> GetFacts(string isoCode, CancellationToken ct)
    {
        var facts = await factsService.GetFactsAsync(isoCode.ToUpperInvariant(), ct);
        if (facts is null) return NotFound();

        return Ok(new CountryFactsDto(
            facts.IsoCode,
            facts.Population,
            facts.GovernmentForm,
            facts.OfficialLanguages,
            facts.AreaKm2,
            facts.Currency,
            facts.MainMountains,
            facts.MainRivers,
            facts.BiodiversityFacts,
            facts.FetchedAt));
    }
}
