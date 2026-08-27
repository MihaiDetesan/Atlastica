using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlastica.Infrastructure.Boundaries;

public class BoundaryRefreshOptions
{
    public int RefreshIntervalDays { get; set; } = 180;
}

/// <summary>
/// Boundaries change rarely, so unlike country facts / POI (refreshed on the request
/// path), this runs a daily check and re-syncs the whole Natural Earth snapshot only
/// once any row is older than <see cref="BoundaryRefreshOptions.RefreshIntervalDays"/>.
/// </summary>
public class BoundaryRefreshHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<BoundaryRefreshOptions> options,
    ILogger<BoundaryRefreshHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            await CheckAndRefreshAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CheckAndRefreshAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AtlasticaDbContext>();
            var fetcher = scope.ServiceProvider.GetRequiredService<NaturalEarthBoundaryFetcher>();

            var hasAny = await db.Countries.AnyAsync(ct);
            var maxAge = TimeSpan.FromDays(options.Value.RefreshIntervalDays);
            var stale = hasAny ? await fetcher.GetStaleAsync(maxAge, ct) : [];

            if (!hasAny || stale.Count > 0)
            {
                logger.LogInformation(
                    "Running boundary sync ({Reason})",
                    hasAny ? $"{stale.Count} stale" : "initial seed");
                await fetcher.SyncAsync(ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Boundary refresh check failed");
        }
    }
}
