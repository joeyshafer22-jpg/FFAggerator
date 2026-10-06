using FFAggerator.Infrastructure.Sleeper;

namespace FFAggerator.Tests;

public class PlayerSearchNormalizeTests
{
    [Theory]
    [InlineData("Patrick Mahomes", "patrickmahomes")]
    [InlineData("A.J. Brown", "ajbrown")]
    [InlineData("Ja'Marr Chase", "jamarrchase")]
    public void NormalizeName_strips_punctuation_and_casing(string input, string expected)
    {
        Assert.Equal(expected, SleeperClient.NormalizeName(input));
    }
}
