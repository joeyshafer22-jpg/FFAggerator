namespace FFAggerator.Domain;

public class NflSeasonState
{
    public int Season { get; set; }
    public int Week { get; set; }
    public int DisplayWeek { get; set; }
    public string SeasonType { get; set; } = "regular";
    public DateTimeOffset FetchedAt { get; set; }
}
