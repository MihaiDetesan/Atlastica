namespace Atlastica.Nlp;

public record CountryFactExtractionResult(
    string? GovernmentForm,
    string? MainMountains,
    string? MainRivers,
    string? BiodiversityFacts);

public record PoiDescriptionResult(string ShortDescription);

public interface IFactExtractionService
{
    Task<CountryFactExtractionResult> ExtractCountryFactsAsync(string countryName, string wikipediaExtract, CancellationToken ct = default);
    Task<PoiDescriptionResult> ExtractPoiDescriptionAsync(string poiName, string wikipediaExtract, CancellationToken ct = default);
}
