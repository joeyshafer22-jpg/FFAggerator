using FFAggerator.Infrastructure.Data;
using FFAggerator.Infrastructure.Rss;
using FFAggerator.Infrastructure.Sleeper;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Infrastructure.Data;

public sealed class DataRefreshHostedService : BackgroundService
{
    private static readonly TimeSpan NewsInterval = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan ProjectionInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan PlayerInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRefreshHostedService> _logger;

    public DataRefreshHostedService(IServiceScopeFactory scopeFactory, ILogger<DataRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAllAsync(stoppingToken);

        var nextNews = DateTimeOffset.UtcNow + NewsInterval;
        var nextProjections = DateTimeOffset.UtcNow + ProjectionInterval;
        var nextPlayers = DateTimeOffset.UtcNow + PlayerInterval;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            try
            {
                if (now >= nextPlayers)
                {
                    await RefreshPlayersAsync(stoppingToken);
                    nextPlayers = now + PlayerInterval;
                }

                if (now >= nextProjections)
                {
                    await RefreshProjectionsAsync(stoppingToken);
                    nextProjections = now + ProjectionInterval;
                }

                if (now >= nextNews)
                {
                    await RefreshNewsAsync(stoppingToken);
                    nextNews = now + NewsInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled data refresh failed.");
            }
        }
    }

    private async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshPlayersAsync(cancellationToken);
            await RefreshProjectionsAsync(cancellationToken);
            await RefreshNewsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial data refresh failed. The API will serve whatever is already cached.");
        }
    }

    private async Task RefreshPlayersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IPlayerCatalog>();
        await catalog.RefreshPlayersAsync(cancellationToken);
    }

    private async Task RefreshProjectionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var projections = scope.ServiceProvider.GetRequiredService<IProjectionProvider>();
        var state = await projections.RefreshStateAsync(cancellationToken);
        await projections.RefreshProjectionsAsync(state.Season, state.Week, state.SeasonType, cancellationToken);
        if (state.Week > 1)
        {
            await projections.RefreshProjectionsAsync(state.Season, state.Week - 1, state.SeasonType, cancellationToken);
        }
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var news = scope.ServiceProvider.GetRequiredService<INewsProvider>();
        await news.RefreshLeagueFeedAsync(cancellationToken);
    }

    public static async Task<DateTimeOffset?> GetMetaTimeAsync(AppDbContext db, string key, CancellationToken cancellationToken)
    {
        var row = await db.Meta.AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, cancellationToken);
        return row is not null && DateTimeOffset.TryParse(row.Value, out var at) ? at : null;
    }
}
