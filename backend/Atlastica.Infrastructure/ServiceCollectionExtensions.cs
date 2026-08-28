using Atlastica.Infrastructure.Boundaries;
using Atlastica.Infrastructure.ExternalFacts;
using Atlastica.Nlp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlastica.Infrastructure;

public static class ServiceCollectionExtensions
{
    // Wikipedia/Wikimedia and Wikidata both ask API consumers to identify
    // themselves; an empty/browser-like UA risks throttling or 403s.
    private const string OutboundUserAgent = "Atlastica/1.0 (https://github.com/; contact: atlastica@example.com)";

    /// <summary>
    /// Everything the API needs: DB access, the boundary-refresh background
    /// job, and a read-only facts lookup. Deliberately does NOT register the
    /// Wikidata/Wikipedia fetchers or the local NLP model - the API never
    /// fetches or extracts facts itself (see AddAtlasticaFactsExtraction,
    /// used only by Atlastica.FactsExtractor), so it doesn't pay the cost of
    /// loading a multi-GB model it will never use.
    /// </summary>
    public static IServiceCollection AddAtlasticaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AtlasticaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<NaturalEarthBoundaryFetcher>();
        services.Configure<BoundaryRefreshOptions>(configuration.GetSection("BoundaryRefresh"));
        services.AddHostedService<BoundaryRefreshHostedService>();

        services.AddScoped<CountryFactsService>();

        return services;
    }

    /// <summary>
    /// The offline fetch+NLP pipeline (Wikidata, Wikipedia, local Phi-3.5
    /// extraction). Used only by Atlastica.FactsExtractor.
    /// </summary>
    public static IServiceCollection AddAtlasticaFactsExtraction(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<WikidataCountryFetcher>(c => c.DefaultRequestHeaders.UserAgent.ParseAdd(OutboundUserAgent));
        services.AddHttpClient<WikipediaFetcher>(c => c.DefaultRequestHeaders.UserAgent.ParseAdd(OutboundUserAgent));

        services.AddSingleton<IFactExtractionService, LocalPhi3ExtractionService>();
        services.Configure<Phi3ModelOptions>(configuration.GetSection("Phi3Model"));

        services.AddScoped<CountryFactsComputer>();

        return services;
    }
}
