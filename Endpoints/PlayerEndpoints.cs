using FFAggerator.Domain;
using FFAggerator.Infrastructure.Data;
using FFAggerator.Infrastructure.Rss;
using FFAggerator.Infrastructure.Sleeper;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Endpoints;

public static class PlayerEndpoints
{
    private static readonly TimeSpan GoogleNewsTtl = TimeSpan.FromMinutes(20);

    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapGet("/api/players", SearchPlayers).WithName("SearchPlayers");
        app.MapGet("/api/players/{id}", GetPlayer).WithName("GetPlayer");
        app.MapGet("/api/players/{id}/projections", GetProjections).WithName("GetPlayerProjections");
        app.MapGet("/api/players/{id}/news", GetNews).WithName("GetPlayerNews");
    }

    private static async Task<IResult> SearchPlayers(
        string? q,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        q = q?.Trim() ?? "";
        if (q.Length < 2)
        {
            return Results.Ok(Array.Empty<PlayerSearchDto>());
        }

        var normalized = SleeperClient.NormalizeName(q);
        var matches = await db.Players.AsNoTracking()
            .Where(p => p.Active && (
                p.FullName.Contains(q) ||
                p.SearchName.Contains(normalized) ||
                (p.Team != null && p.Team.Contains(q))))
            .OrderBy(p => p.SearchRank ?? int.MaxValue)
            .ThenBy(p => p.FullName)
            .Take(20)
            .Select(p => new PlayerSearchDto(
                p.Id,
                p.FullName,
                p.Team,
                p.Position,
                p.InjuryStatus,
                p.Status,
                p.Number,
                p.SearchRank))
            .ToListAsync(cancellationToken);

        return Results.Ok(matches);
    }

    private static async Task<IResult> GetPlayer(
        string id,
        AppDbContext db,
        INflStateProvider stateProvider,
        CancellationToken cancellationToken)
    {
        var player = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (player is null)
        {
            return Results.NotFound();
        }

        var state = await stateProvider.GetCachedStateAsync(cancellationToken);
        return Results.Ok(new PlayerDetailDto(
            player.Id,
            player.FullName,
            player.FirstName,
            player.LastName,
            player.Team,
            player.Position,
            player.InjuryStatus,
            player.Status,
            player.Number,
            player.Active,
            player.UpdatedAt,
            state?.Season,
            state?.Week,
            state?.SeasonType));
    }

    private static async Task<IResult> GetProjections(
        string id,
        int? week,
        AppDbContext db,
        INflStateProvider stateProvider,
        CancellationToken cancellationToken)
    {
        var player = await db.Players.AsNoTracking().AnyAsync(p => p.Id == id, cancellationToken);
        if (!player)
        {
            return Results.NotFound();
        }

        var state = await stateProvider.GetCachedStateAsync(cancellationToken);
        if (state is null)
        {
            return Results.Ok(new ProjectionResponseDto(id, null, week, null, true, null, null, null, "Season state is not cached yet."));
        }

        var requestedWeek = week ?? state.Week;
        var current = await LoadProjectionAsync(db, id, state.Season, requestedWeek, state.SeasonType, cancellationToken);
        WeeklyProjectionDto? fallback = null;
        if (current is null && requestedWeek > 1)
        {
            var previous = await LoadProjectionAsync(db, id, state.Season, requestedWeek - 1, state.SeasonType, cancellationToken);
            if (previous is not null)
            {
                fallback = previous;
            }
        }

        var empty = current is null;
        var message = empty
            ? requestedWeek == state.Week
                ? "No projection for the current week (bye, offseason, or not yet published)."
                : "No projection for the requested week."
            : null;

        return Results.Ok(new ProjectionResponseDto(
            id,
            state.Season,
            requestedWeek,
            state.SeasonType,
            empty,
            current,
            fallback,
            current?.FetchedAt ?? fallback?.FetchedAt,
            message));
    }

    private static async Task<IResult> GetNews(
        string id,
        int? limit,
        AppDbContext db,
        INewsProvider newsProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (player is null)
        {
            return Results.NotFound();
        }

        var take = Math.Clamp(limit ?? 20, 1, 50);
        var lastGoogle = await DataRefreshHostedService.GetMetaTimeAsync(db, $"news.google.{player.Id}", cancellationToken);
        if (lastGoogle is null || DateTimeOffset.UtcNow - lastGoogle > GoogleNewsTtl)
        {
            try
            {
                await newsProvider.RefreshPlayerFeedAsync(player, cancellationToken);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("PlayerNews").LogWarning(ex, "Google News refresh failed for {PlayerId}.", id);
            }
        }

        var items = await db.News.AsNoTracking()
            .Where(n => n.PlayerId == id)
            .OrderByDescending(n => n.PublishedAt ?? n.FetchedAt)
            .Take(take)
            .Select(n => new NewsItemDto(n.Title, n.Url, n.Source, n.PublishedAt, n.Summary, n.FetchedAt))
            .ToListAsync(cancellationToken);

        var espnFetched = await DataRefreshHostedService.GetMetaTimeAsync(db, "news.espn.fetchedAt", cancellationToken);
        var googleFetched = await DataRefreshHostedService.GetMetaTimeAsync(db, $"news.google.{player.Id}", cancellationToken);

        return Results.Ok(new NewsResponseDto(id, items, espnFetched, googleFetched, items.Count == 0));
    }

    private static async Task<WeeklyProjectionDto?> LoadProjectionAsync(
        AppDbContext db,
        string playerId,
        int season,
        int week,
        string seasonType,
        CancellationToken cancellationToken)
    {
        var row = await db.Projections.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.PlayerId == playerId && p.Season == season && p.Week == week && p.SeasonType == seasonType,
                cancellationToken);

        return row is null
            ? null
            : new WeeklyProjectionDto(row.Week, row.Season, row.SeasonType, row.PtsPpr, row.PtsHalf, row.PtsStd, row.FetchedAt);
    }
}

public sealed record PlayerSearchDto(
    string Id,
    string FullName,
    string? Team,
    string? Position,
    string? InjuryStatus,
    string? Status,
    int? Number,
    int? SearchRank);

public sealed record PlayerDetailDto(
    string Id,
    string FullName,
    string FirstName,
    string LastName,
    string? Team,
    string? Position,
    string? InjuryStatus,
    string? Status,
    int? Number,
    bool Active,
    DateTimeOffset UpdatedAt,
    int? Season,
    int? Week,
    string? SeasonType);

public sealed record WeeklyProjectionDto(
    int Week,
    int Season,
    string SeasonType,
    double? PtsPpr,
    double? PtsHalf,
    double? PtsStd,
    DateTimeOffset FetchedAt);

public sealed record ProjectionResponseDto(
    string PlayerId,
    int? Season,
    int? Week,
    string? SeasonType,
    bool IsEmpty,
    WeeklyProjectionDto? Current,
    WeeklyProjectionDto? Fallback,
    DateTimeOffset? FetchedAt,
    string? Message);

public sealed record NewsItemDto(
    string Title,
    string Url,
    string Source,
    DateTimeOffset? PublishedAt,
    string? Summary,
    DateTimeOffset FetchedAt);

public sealed record NewsResponseDto(
    string PlayerId,
    IReadOnlyList<NewsItemDto> Items,
    DateTimeOffset? EspnFetchedAt,
    DateTimeOffset? GoogleFetchedAt,
    bool IsEmpty);
