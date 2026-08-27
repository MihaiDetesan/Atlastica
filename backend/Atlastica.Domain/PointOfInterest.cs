namespace Atlastica.Domain;

public class PointOfInterest
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ShortDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string? WikipediaUrl { get; set; }
    public DateTimeOffset? FetchedAt { get; set; }
}
