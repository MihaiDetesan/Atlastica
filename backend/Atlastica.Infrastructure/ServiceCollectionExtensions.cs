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

    public static IServiceCollection AddAtlasticaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AtlasticaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<NaturalEarthBoundaryFetcher>();
        services.Configure<BoundaryRefreshOptions>(configuration.GetSection("BoundaryRefresh"));
        services.AddHostedService<BoundaryRefreshHostedService>();

        services.AddHttpClient<WikidataCountryFetcher>(c => c.DefaultRequestHeaders.UserAgent.ParseAdd(OutboundUserAgent));
        services.AddHttpClient<WikipediaFetcher>(c => c.DefaultRequestHeaders.UserAgent.ParseAdd(OutboundUserAgent));

        services.Configure<FactsCacheOptions>(configuration.GetSection("FactsCache"));
        services.AddScoped<CountryFactsService>();

        services.AddSingleton<IFactExtractionService, LocalPhi3ExtractionService>();
        services.Configure<Phi3ModelOptions>(configuration.GetSection("Phi3Model"));

        return services;
    }
}
