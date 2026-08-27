using System.Text.Json;
using Atlastica.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlastica.Infrastructure.Boundaries;

/// <summary>
/// Seeds/refreshes the `countries` table from the bundled Natural Earth Admin-0
/// countries snapshot (public domain). Boundaries change rarely, so this is a
/// periodic bulk sync rather than a per-request cache lookup.
/// </summary>
public class NaturalEarthBoundaryFetcher(AtlasticaDbContext db, ILogger<NaturalEarthBoundaryFetcher> logger)
{
    private static readonly string SeedFilePath = Path.Combine(
        AppContext.BaseDirectory, "SeedData", "ne_110m_admin_0_countries.geojson");

    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(SeedFilePath, ct);
        using var doc = JsonDocument.Parse(json);
        var features = doc.RootElement.GetProperty("features");

        var now = DateTimeOffset.UtcNow;
        var synced = 0;

        foreach (var feature in features.EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var isoCode = ResolveIsoCode(props);
            if (isoCode is null)
            {
                logger.LogWarning("Skipping feature with no resolvable ISO code: {Name}", props.GetProperty("NAME").GetString());
                continue;
            }

            var name = props.GetProperty("NAME").GetString()!;
            var geometry = feature.GetProperty("geometry").GetRawText();

            var existing = await db.Countries.FindAsync([isoCode], ct);
            if (existing is null)
            {
                db.Countries.Add(new Country
                {
                    IsoCode = isoCode,
                    Name = name,
                    BoundaryGeoJson = geometry,
                    BoundaryFetchedAt = now,
                });
            }
            else
            {
                existing.Name = name;
                existing.BoundaryGeoJson = geometry;
                existing.BoundaryFetchedAt = now;
            }

            synced++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Synced {Count} country boundaries from Natural Earth", synced);
        return synced;
    }

    /// <summary>
    /// A handful of disputed/unrecognized territories have no ISO 3166-1 alpha-2 code
    /// in Natural Earth (e.g. N. Cyprus, Somaliland) — fall back to the dataset's own
    /// 3-letter admin code so they still get a stable, unique key.
    /// </summary>
    private static string? ResolveIsoCode(JsonElement props)
    {
        var iso2 = props.GetProperty("ISO_A2").GetString();
        if (!IsPlaceholder(iso2)) return iso2;

        var iso2Eh = props.GetProperty("ISO_A2_EH").GetString();
        if (!IsPlaceholder(iso2Eh)) return iso2Eh;

        var adm0A3 = props.GetProperty("ADM0_A3").GetString();
        return IsPlaceholder(adm0A3) ? null : adm0A3;
    }

    private static bool IsPlaceholder(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "-99";

    public async Task<IReadOnlyList<Country>> GetStaleAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;
        return await db.Countries.Where(c => c.BoundaryFetchedAt < cutoff).ToListAsync(ct);
    }
}
