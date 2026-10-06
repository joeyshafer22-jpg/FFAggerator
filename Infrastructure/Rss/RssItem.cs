namespace FFAggerator.Infrastructure.Rss;

public sealed record RssItem(
    string Title,
    string Url,
    string Source,
    DateTimeOffset? PublishedAt,
    string? Summary);
