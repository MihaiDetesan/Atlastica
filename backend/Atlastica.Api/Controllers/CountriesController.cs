using System.Text.Json;
using Atlastica.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlastica.Api.Controllers;

public record CountryFeatureDto(string IsoCode, string Name, JsonElement Geometry);

[ApiController]
[Route("api/countries")]
public class CountriesController(AtlasticaDbContext db) : ControllerBase
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
}
