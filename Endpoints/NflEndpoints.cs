using FFAggerator.Infrastructure.Data;
using FFAggerator.Infrastructure.Sleeper;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Endpoints;

public static class NflEndpoints
{
    public static void MapNflEndpoints(this WebApplication app)
    {
        app.MapGet("/api/nfl/state", GetState).WithName("GetNflState");
    }

    private static async Task<IResult> GetState(
        AppDbContext db,
        INflStateProvider stateProvider,
        CancellationToken cancellationToken)
    {
        var state = await stateProvider.GetCachedStateAsync(cancellationToken);
        var playersFetched = await DataRefreshHostedService.GetMetaTimeAsync(db, "players.fetchedAt", cancellationToken);
        var espnFetched = await DataRefreshHostedService.GetMetaTimeAsync(db, "news.espn.fetchedAt", cancellationToken);
        var playerCount = await db.Players.CountAsync(p => p.Active, cancellationToken);

        return Results.Ok(new
        {
            season = state?.Season,
            week = state?.Week,
            displayWeek = state?.DisplayWeek,
            seasonType = state?.SeasonType,
            fetchedAt = state?.FetchedAt,
            playersFetchedAt = playersFetched,
            newsFetchedAt = espnFetched,
            activePlayerCount = playerCount,
            ready = state is not null && playerCount > 0
        });
    }
}
