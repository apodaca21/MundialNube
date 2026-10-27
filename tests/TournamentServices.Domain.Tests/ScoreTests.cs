using TournamentServices.Domain;

namespace TournamentServices.Domain.Tests;

public class ScoreTests
{
    [Fact]
    public void Score_AcceptsZeroValues()
    {
        var score = new Score();

        Assert.Equal(0, score.HomeTeamScore);
        Assert.Equal(0, score.VisitorTeamScore);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -2)]
    public void Score_RejectsNegativeValues(int home, int visitor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Score(home, visitor));
    }
}
