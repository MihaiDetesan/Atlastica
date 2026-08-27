namespace Atlastica.Domain;

public class Country
{
    public required string IsoCode { get; set; }
    public required string Name { get; set; }
    public required string BoundaryGeoJson { get; set; }
    public DateTimeOffset BoundaryFetchedAt { get; set; }

    public CountryFacts? Facts { get; set; }
}
