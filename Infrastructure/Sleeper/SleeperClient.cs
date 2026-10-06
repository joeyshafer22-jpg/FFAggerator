using System.Net.Http.Json;
using System.Text.Json;
using FFAggerator.Domain;
using FFAggerator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FFAggerator.Infrastructure.Sleeper;

public sealed class SleeperClient : IPlayerCatalog, IProjectionProvider, INflStateProvider
{
    public const string HttpClientName = "Sleeper";

    private static readonly HashSet<string> FantasyPositions =
        new(StringComparer.OrdinalIgnoreCase) { "QB", "RB", "WR", "TE", "K", "DEF" };

    private readonly IHttpClientFactory _httpFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<SleeperClient> _logger;

    public SleeperClient(IHttpClientFactory httpFactory, AppDbContext db, ILogger<SleeperClient> logger)
    {
        _httpFactory = httpFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<NflSeasonState?> GetCachedStateAsync(CancellationToken cancellationToken)
    {
        var season = await GetMetaAsync("nfl.season", cancellationToken);
        var week = await GetMetaAsync("nfl.week", cancellationToken);
        var displayWeek = await GetMetaAsync("nfl.displayWeek", cancellationToken);
        var seasonType = await GetMetaAsync("nfl.seasonType", cancellationToken);
        var fetched = await GetMetaAsync("nfl.fetchedAt", cancellationToken);

        if (season is null || week is null || seasonType is null)
        {
            return null;
        }

        return new NflSeasonState
        {
            Season = int.Parse(season),
            Week = int.Parse(week),
            DisplayWeek = int.TryParse(displayWeek, out var dw) ? dw : int.Parse(week),
            SeasonType = seasonType,
            FetchedAt = DateTimeOffset.TryParse(fetched, out var at) ? at : DateTimeOffset.UtcNow
        };
    }

    public async Task<NflSeasonState> RefreshStateAsync(CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        using var doc = await client.GetFromJsonAsync<JsonDocument>("state/nfl", cancellationToken)
            ?? throw new InvalidOperationException("Sleeper NFL state was empty.");

        var root = doc.RootElement;
        var state = new NflSeasonState
        {
            Season = ParseInt(root, "season") ?? DateTime.UtcNow.Year,
            Week = ParseInt(root, "week") ?? 1,
            DisplayWeek = ParseInt(root, "display_week") ?? ParseInt(root, "week") ?? 1,
            SeasonType = root.TryGetProperty("season_type", out var st) ? st.GetString() ?? "regular" : "regular",
            FetchedAt = DateTimeOffset.UtcNow
        };

        await SetMetaAsync("nfl.season", state.Season.ToString(), cancellationToken);
        await SetMetaAsync("nfl.week", state.Week.ToString(), cancellationToken);
        await SetMetaAsync("nfl.displayWeek", state.DisplayWeek.ToString(), cancellationToken);
        await SetMetaAsync("nfl.seasonType", state.SeasonType, cancellationToken);
        await SetMetaAsync("nfl.fetchedAt", state.FetchedAt.ToString("O"), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return state;
    }

    public async Task<int> RefreshPlayersAsync(CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        using var doc = await client.GetFromJsonAsync<JsonDocument>("players/nfl", cancellationToken)
            ?? throw new InvalidOperationException("Sleeper players payload was empty.");

        var now = DateTimeOffset.UtcNow;
        var incoming = new Dictionary<string, Player>(StringComparer.Ordinal);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!TryMapPlayer(prop.Name, prop.Value, now, out var player))
            {
                continue;
            }

            incoming[player.Id] = player;
        }

        var existing = await _db.Players.ToDictionaryAsync(p => p.Id, cancellationToken);
        foreach (var player in incoming.Values)
        {
            if (existing.TryGetValue(player.Id, out var row))
            {
                row.FirstName = player.FirstName;
                row.LastName = player.LastName;
                row.FullName = player.FullName;
                row.Team = player.Team;
                row.Position = player.Position;
                row.Status = player.Status;
                row.InjuryStatus = player.InjuryStatus;
                row.Number = player.Number;
                row.Active = player.Active;
                row.SearchRank = player.SearchRank;
                row.SearchName = player.SearchName;
                row.UpdatedAt = now;
            }
            else
            {
                _db.Players.Add(player);
            }
        }

        foreach (var stale in existing.Values.Where(p => !incoming.ContainsKey(p.Id)))
        {
            stale.Active = false;
            stale.UpdatedAt = now;
        }

        await SetMetaAsync("players.fetchedAt", now.ToString("O"), cancellationToken);
        var saved = await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refreshed {Count} fantasy-relevant NFL players from Sleeper.", incoming.Count);
        return incoming.Count;
    }

    public async Task<int> RefreshProjectionsAsync(int season, int week, string seasonType, CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        var path = $"projections/nfl/{Uri.EscapeDataString(seasonType)}/{season}/{week}";
        using var doc = await client.GetFromJsonAsync<JsonDocument>(path, cancellationToken)
            ?? throw new InvalidOperationException("Sleeper projections payload was empty.");

        var now = DateTimeOffset.UtcNow;
        var knownPlayers = await _db.Players.Select(p => p.Id).ToHashSetAsync(cancellationToken);
        var existing = await _db.Projections
            .Where(p => p.Season == season && p.Week == week && p.SeasonType == seasonType)
            .ToDictionaryAsync(p => p.PlayerId, cancellationToken);

        var upserts = 0;
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object || !knownPlayers.Contains(prop.Name))
            {
                continue;
            }

            var stats = prop.Value;
            var ptsPpr = GetDouble(stats, "pts_ppr");
            var ptsHalf = GetDouble(stats, "pts_half_ppr");
            var ptsStd = GetDouble(stats, "pts_std");
            if (ptsPpr is null && ptsHalf is null && ptsStd is null)
            {
                continue;
            }

            if (existing.TryGetValue(prop.Name, out var row))
            {
                row.PtsPpr = ptsPpr;
                row.PtsHalf = ptsHalf;
                row.PtsStd = ptsStd;
                row.StatsJson = stats.GetRawText();
                row.FetchedAt = now;
            }
            else
            {
                _db.Projections.Add(new WeeklyProjection
                {
                    PlayerId = prop.Name,
                    Season = season,
                    Week = week,
                    SeasonType = seasonType,
                    PtsPpr = ptsPpr,
                    PtsHalf = ptsHalf,
                    PtsStd = ptsStd,
                    StatsJson = stats.GetRawText(),
                    FetchedAt = now
                });
            }

            upserts++;
        }

        await SetMetaAsync($"projections.{seasonType}.{season}.{week}", now.ToString("O"), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refreshed {Count} projections for {SeasonType} {Season} week {Week}.", upserts, seasonType, season, week);
        return upserts;
    }

    private static bool TryMapPlayer(string id, JsonElement json, DateTimeOffset now, out Player player)
    {
        player = null!;
        var position = GetString(json, "position");
        var fantasyPositions = json.TryGetProperty("fantasy_positions", out var fp) && fp.ValueKind == JsonValueKind.Array
            ? fp.EnumerateArray().Select(e => e.GetString()).Where(s => s is not null).Cast<string>().ToArray()
            : [];

        var resolvedPosition = position;
        if (string.IsNullOrEmpty(resolvedPosition) || !FantasyPositions.Contains(resolvedPosition))
        {
            resolvedPosition = fantasyPositions.FirstOrDefault(p => FantasyPositions.Contains(p));
        }

        if (resolvedPosition is null || !FantasyPositions.Contains(resolvedPosition))
        {
            return false;
        }

        var active = json.TryGetProperty("active", out var activeEl) && activeEl.ValueKind == JsonValueKind.True;
        var first = GetString(json, "first_name") ?? "";
        var last = GetString(json, "last_name") ?? "";
        var full = GetString(json, "full_name") ?? $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(full))
        {
            return false;
        }

        player = new Player
        {
            Id = id,
            FirstName = first,
            LastName = last,
            FullName = full,
            Team = GetString(json, "team"),
            Position = resolvedPosition,
            Status = GetString(json, "status"),
            InjuryStatus = GetString(json, "injury_status"),
            Number = ParseInt(json, "number"),
            Active = active,
            SearchRank = ParseInt(json, "search_rank"),
            SearchName = NormalizeName(full),
            UpdatedAt = now
        };
        return true;
    }

    public static string NormalizeName(string value) =>
        new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private async Task<string?> GetMetaAsync(string key, CancellationToken cancellationToken)
    {
        var row = await _db.Meta.AsNoTracking().FirstOrDefaultAsync(m => m.Key == key, cancellationToken);
        return row?.Value;
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

    private static string? GetString(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static int? ParseInt(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(el.GetString(), out var n) => n,
            _ => null
        };
    }

    private static double? GetDouble(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetDouble(out var n) => n,
            JsonValueKind.String when double.TryParse(el.GetString(), out var n) => n,
            _ => null
        };
    }
}
