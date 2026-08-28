namespace Atlastica.Domain;

public class CountryFacts
{
    public required string IsoCode { get; set; }
    public long? Population { get; set; }
    public string? Capital { get; set; }
    public string? GovernmentForm { get; set; }
    public List<string> OfficialLanguages { get; set; } = [];
    public double? AreaKm2 { get; set; }
    public string? Currency { get; set; }
    public string? MainMountains { get; set; }
    public string? MainRivers { get; set; }
    public string? BiodiversityFacts { get; set; }
    public string? SourceExtractHash { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Country? Country { get; set; }
}
