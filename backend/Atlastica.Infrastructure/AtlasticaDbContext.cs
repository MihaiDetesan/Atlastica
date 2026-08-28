using Atlastica.Domain;
using Microsoft.EntityFrameworkCore;

namespace Atlastica.Infrastructure;

public class AtlasticaDbContext(DbContextOptions<AtlasticaDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CountryFacts> CountryFacts => Set<CountryFacts>();
    public DbSet<PointOfInterest> PointsOfInterest => Set<PointOfInterest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Country>(e =>
        {
            e.ToTable("countries");
            e.HasKey(c => c.IsoCode);
            e.Property(c => c.IsoCode).HasColumnName("iso_code");
            e.Property(c => c.Name).HasColumnName("name");
            e.Property(c => c.BoundaryGeoJson).HasColumnName("boundary_geojson").HasColumnType("jsonb");
            e.Property(c => c.BoundaryFetchedAt).HasColumnName("boundary_fetched_at");
        });

        modelBuilder.Entity<CountryFacts>(e =>
        {
            e.ToTable("country_facts");
            e.HasKey(f => f.IsoCode);
            e.Property(f => f.IsoCode).HasColumnName("iso_code");
            e.Property(f => f.Population).HasColumnName("population");
            e.Property(f => f.Capital).HasColumnName("capital");
            e.Property(f => f.GovernmentForm).HasColumnName("government_form");
            e.Property(f => f.OfficialLanguages).HasColumnName("official_languages");
            e.Property(f => f.AreaKm2).HasColumnName("area_km2");
            e.Property(f => f.Currency).HasColumnName("currency");
            e.Property(f => f.MainMountains).HasColumnName("main_mountains");
            e.Property(f => f.MainRivers).HasColumnName("main_rivers");
            e.Property(f => f.BiodiversityFacts).HasColumnName("biodiversity_facts");
            e.Property(f => f.SourceExtractHash).HasColumnName("source_extract_hash");
            e.Property(f => f.FetchedAt).HasColumnName("fetched_at");

            e.HasOne(f => f.Country)
                .WithOne(c => c.Facts)
                .HasForeignKey<CountryFacts>(f => f.IsoCode);
        });

        modelBuilder.Entity<PointOfInterest>(e =>
        {
            e.ToTable("points_of_interest");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.Name).HasColumnName("name");
            e.Property(p => p.Slug).HasColumnName("slug");
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Category).HasColumnName("category");
            e.Property(p => p.Latitude).HasColumnName("latitude");
            e.Property(p => p.Longitude).HasColumnName("longitude");
            e.Property(p => p.ShortDescription).HasColumnName("short_description");
            e.Property(p => p.ImageUrl).HasColumnName("image_url");
            e.Property(p => p.WikipediaUrl).HasColumnName("wikipedia_url");
            e.Property(p => p.FetchedAt).HasColumnName("fetched_at");
        });
    }
}
