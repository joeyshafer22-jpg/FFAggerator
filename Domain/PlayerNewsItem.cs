namespace FFAggerator.Domain;

public class PlayerNewsItem
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public string? Summary { get; set; }
    public int MatchScore { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Player? Player { get; set; }
}
