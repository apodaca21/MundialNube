using TournamentServices.Domain;
using TournamentServices.Domain.Enums;

namespace TournamentServices.Domain.Tests;

public class MatchTests
{
    [Fact]
    public void NewMatch_StartsWithoutWinner()
    {
        var match = new Match("match-1", "cup-1", "group-1", "home-1", "away-1");

        Assert.False(match.IsCompleted);
        Assert.Null(match.Winner);
        Assert.Equal(0, match.Score.HomeTeamScore);
        Assert.Equal(MatchStages.Group, match.Stage);
    }

    [Fact]
    public void UpdateScore_SetsHomeWinner()
    {
        var match = new Match("match-1", "cup-1", null, "home-1", "away-1");

        match.UpdateScore(new Score(2, 1));

        Assert.Equal(Winner.HOME, match.Winner);
        Assert.True(match.IsCompleted);
    }

    [Fact]
    public void UpdateScore_SetsVisitorWinner()
    {
        var match = new Match("match-1", "cup-1", null, "home-1", "away-1");

        match.UpdateScore(new Score(0, 3));

        Assert.Equal(Winner.VISITOR, match.Winner);
    }

    [Fact]
    public void UpdateScore_KeepsWinnerNullOnDraw()
    {
        var match = new Match("match-1", "cup-1", null, "home-1", "away-1");

        match.UpdateScore(new Score(1, 1));

        Assert.Null(match.Winner);
        Assert.True(match.IsCompleted);
    }
}
