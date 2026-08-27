using Atlastica.Infrastructure.Boundaries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Atlastica.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasticaInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AtlasticaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<NaturalEarthBoundaryFetcher>();
        services.Configure<BoundaryRefreshOptions>(configuration.GetSection("BoundaryRefresh"));
        services.AddHostedService<BoundaryRefreshHostedService>();

        return services;
    }
}
