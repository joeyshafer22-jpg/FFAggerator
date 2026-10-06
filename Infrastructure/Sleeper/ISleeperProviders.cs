using FFAggerator.Domain;

namespace FFAggerator.Infrastructure.Sleeper;

public interface IPlayerCatalog
{
    Task<int> RefreshPlayersAsync(CancellationToken cancellationToken);
}

public interface IProjectionProvider
{
    Task<NflSeasonState> RefreshStateAsync(CancellationToken cancellationToken);
    Task<int> RefreshProjectionsAsync(int season, int week, string seasonType, CancellationToken cancellationToken);
}

public interface INflStateProvider
{
    Task<NflSeasonState?> GetCachedStateAsync(CancellationToken cancellationToken);
}
