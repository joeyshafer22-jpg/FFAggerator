namespace FFAggerator.Domain;

public class WeeklyProjection
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = "";
    public int Season { get; set; }
    public int Week { get; set; }
    public string SeasonType { get; set; } = "regular";
    public double? PtsPpr { get; set; }
    public double? PtsHalf { get; set; }
    public double? PtsStd { get; set; }
    public string? StatsJson { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public Player? Player { get; set; }
}
