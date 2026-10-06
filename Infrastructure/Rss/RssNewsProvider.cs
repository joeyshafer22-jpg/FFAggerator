using FFAggerator.Domain;
using FFAggerator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Infrastructure.Rss;

public interface INewsProvider
{
    Task<int> RefreshLeagueFeedAsync(CancellationToken cancellationToken);
    Task<int> RefreshPlayerFeedAsync(Player player, CancellationToken cancellationToken);
}

public sealed class RssNewsProvider : INewsProvider
{
    public const string EspnNflFeed = "https://www.espn.com/espn/rss/nfl/news";

    private readonly INewsFeedReader _feedReader;
    private readonly PlayerNewsMatcher _matcher;
    private readonly AppDbContext _db;
    private readonly ILogger<RssNewsProvider> _logger;

    public RssNewsProvider(
        INewsFeedReader feedReader,
        PlayerNewsMatcher matcher,
        AppDbContext db,
        ILogger<RssNewsProvider> logger)
    {
        _feedReader = feedReader;
        _matcher = matcher;
        _db = db;
        _logger = logger;
    }

    public async Task<int> RefreshLeagueFeedAsync(CancellationToken cancellationToken)
    {
        var items = await _feedReader.FetchAsync(EspnNflFeed, cancellationToken);
        var players = await _db.Players.AsNoTracking().Where(p => p.Active).ToListAsync(cancellationToken);
        var saved = await SaveMatchesAsync(items, players, "ESPN", cancellationToken);
        await SetMetaAsync("news.espn.fetchedAt", DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Matched {Count} ESPN NFL stories to players.", saved);
        return saved;
    }

    public async Task<int> RefreshPlayerFeedAsync(Player player, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"\"{player.FullName}\" {player.Team} NFL");
        var url = $"https://news.google.com/rss/search?q={query}&hl=en-US&gl=US&ceid=US:en";
        var items = await _feedReader.FetchAsync(url, cancellationToken);
        var saved = await SaveMatchesAsync(items, [player], "Google News", cancellationToken);
        await SetMetaAsync($"news.google.{player.Id}", DateTimeOffset.UtcNow.ToString("O"), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return saved;
    }

    private async Task<int> SaveMatchesAsync(
        IReadOnlyList<RssItem> items,
        IReadOnlyList<Player> players,
        string fallbackSource,
        CancellationToken cancellationToken)
    {
        var saved = 0;
        var now = DateTimeOffset.UtcNow;
        var existing = await _db.News.AsNoTracking()
            .Select(n => n.PlayerId + "\n" + n.Url)
            .ToHashSetAsync(cancellationToken);

        foreach (var item in items)
        {
            var haystack = $"{item.Title} {item.Summary}".ToLowerInvariant();
            foreach (var player in players)
            {
                if (player.LastName.Length < 3 ||
                    !haystack.Contains(player.LastName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var score = _matcher.Score(player, item.Title, item.Summary);
                if (!_matcher.IsMatch(score))
                {
                    continue;
                }

                var key = player.Id + "\n" + item.Url;
                if (!existing.Add(key))
                {
                    continue;
                }

                _db.News.Add(new PlayerNewsItem
                {
                    PlayerId = player.Id,
                    Title = Truncate(item.Title, 500),
                    Url = item.Url,
                    Source = string.IsNullOrWhiteSpace(item.Source) ? fallbackSource : Truncate(item.Source, 120),
                    PublishedAt = item.PublishedAt,
                    Summary = item.Summary is null ? null : Truncate(StripTags(item.Summary), 1000),
                    MatchScore = score,
                    FetchedAt = now
                });
                saved++;
            }
        }

        return saved;
    }

    private async Task SetMetaAsync(string key, string value, CancellationToken cancellationToken)
    {
        var row = await _db.Meta.FindAsync([key], cancellationToken);
        if (row is null)
        {
            _db.Meta.Add(new CacheMeta { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string StripTags(string html)
    {
        var chars = new char[html.Length];
        var inTag = false;
        var n = 0;
        foreach (var c in html)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (c == '>')
            {
                inTag = false;
                continue;
            }

            if (!inTag)
            {
                chars[n++] = c;
            }
        }

        return new string(chars, 0, n).Trim();
    }
}
