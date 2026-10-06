using FFAggerator.Domain;
using FFAggerator.Infrastructure.Rss;

namespace FFAggerator.Tests;

public class PlayerNewsMatcherTests
{
    private readonly PlayerNewsMatcher _matcher = new();

    [Fact]
    public void Scores_full_name_and_team_as_a_strong_match()
    {
        var player = JoshAllen();
        var score = _matcher.Score(player, "Josh Allen leads Bills past Dolphins", "Buffalo QB Josh Allen threw three touchdowns.");
        Assert.True(_matcher.IsMatch(score));
        Assert.True(score >= 10);
    }

    [Fact]
    public void Rejects_unrelated_aliens_headline()
    {
        var score = _matcher.Score(JoshAllen(), "Scientists spot aliens near Jupiter", "NASA discusses unexplained objects.");
        Assert.False(_matcher.IsMatch(score));
        Assert.Equal(0, score);
    }

    [Fact]
    public void Rejects_other_players_named_Josh()
    {
        var score = _matcher.Score(JoshAllen(), "Josh Jacobs runs over the defense", "Raiders running back Josh Jacobs scored twice.");
        Assert.False(_matcher.IsMatch(score));
    }

    [Fact]
    public void Last_name_plus_team_can_match_without_first_name()
    {
        var player = new Player
        {
            FirstName = "Patrick",
            LastName = "Mahomes",
            FullName = "Patrick Mahomes",
            Team = "KC",
            Position = "QB"
        };

        var score = _matcher.Score(player, "Mahomes and KC stay unbeaten", null);
        Assert.True(_matcher.IsMatch(score));
    }

    private static Player JoshAllen() => new()
    {
        FirstName = "Josh",
        LastName = "Allen",
        FullName = "Josh Allen",
        Team = "BUF",
        Position = "QB"
    };
}
