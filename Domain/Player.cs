namespace FFAggerator.Domain;

public class Player
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Team { get; set; }
    public string? Position { get; set; }
    public string? Status { get; set; }
    public string? InjuryStatus { get; set; }
    public int? Number { get; set; }
    public bool Active { get; set; }
    public int? SearchRank { get; set; }
    public string SearchName { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}
