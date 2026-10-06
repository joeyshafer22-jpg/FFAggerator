using System.Text.RegularExpressions;
using FFAggerator.Domain;

namespace FFAggerator.Infrastructure.Rss;

public sealed class PlayerNewsMatcher
{
    public const int MinimumScore = 8;

    public int Score(Player player, string title, string? description)
    {
        var text = $"{title} {description}".ToLowerInvariant();
        var full = player.FullName.Trim();
        if (full.Length < 4)
        {
            return 0;
        }

        var score = 0;
        if (ContainsPhrase(text, full.ToLowerInvariant()))
        {
            score += 10;
        }
        else
        {
            var first = player.FirstName.Trim().ToLowerInvariant();
            var last = player.LastName.Trim().ToLowerInvariant();
            if (last.Length >= 3 && ContainsWord(text, first) && ContainsWord(text, last))
            {
                score += 8;
            }
            else if (last.Length >= 5 && ContainsWord(text, last) && ContainsWord(text, player.Team?.ToLowerInvariant() ?? ""))
            {
                score += 5;
            }
            else
            {
                return 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(player.Team) && ContainsWord(text, player.Team.ToLowerInvariant()))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(player.Position) && ContainsWord(text, player.Position.ToLowerInvariant()))
        {
            score += 1;
        }

        return score;
    }

    public bool IsMatch(int score) => score >= MinimumScore;

    private static bool ContainsPhrase(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        return text.Contains(phrase, StringComparison.Ordinal);
    }

    private static bool ContainsWord(string text, string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
        {
            return false;
        }

        return Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
