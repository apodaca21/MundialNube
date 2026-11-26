using TournamentServices.Domain;

namespace TournamentServices.Domain.Tests;

public class WorldCupRulesTests
{
    [Fact]
    public void Standings_PreserveDraws_AndCountBothSidesGoals()
    {
        Team[] teams = [new() { Id = "a", Name = "Alpha" }, new() { Id = "b", Name = "Beta" }];
        var drawn = new Match("draw", "cup", "group", "a", "b");
        drawn.UpdateScore(new Score(2, 2));
        var incomplete = new Match("empty", "cup", "group", "a", "b");
        var knockout = new Match("knockout", "cup", null, "a", "b", 1);
        knockout.UpdateScore(new Score(5, 0));
        var rows = WorldCupRules.Standings(teams, [drawn, incomplete, knockout]);
        Assert.All(rows, row =>
        {
            Assert.Equal(1, row.Played); Assert.Equal(1, row.Draws); Assert.Equal(0, row.Wins);
            Assert.Equal(2, row.GoalsFor); Assert.Equal(2, row.GoalsAgainst); Assert.Equal(1, row.Points);
        });
        Assert.Null(drawn.Winner);
    }

    [Fact]
    public void Order_UsesWinsThenGoalsForThenGoalsAgainstThenCaseInsensitiveName()
    {
        TeamStanding Row(string id, string name, int wins, int gf, int ga) => new(id, name, 3, wins, 0, 0, gf, ga);
        var sorted = WorldCupRules.Order([Row("x", "zulu", 1, 3, 1), Row("c", "Zulu", 1, 4, 1),
            Row("b", "alpha", 1, 4, 1), Row("a", "Beta", 1, 4, 0), Row("d", "Delta", 2, 0, 8)]);
        Assert.Equal(new[] { "d", "a", "b", "c", "x" }, sorted.Select(row => row.TeamId));
    }

    [Fact]
    public void Pair_AssignsByesToBestSeeds_AndPairsBestRemainingWithWorst()
    {
        var result = WorldCupRules.Pair(["1", "2", "3", "4", "5", "6"]);
        Assert.Equal(new[] { "1", "2" }, result.Byes);
        Assert.Equal(new[] { new KnockoutPair("3", "6"), new KnockoutPair("4", "5") }, result.Matches);
    }

    [Fact]
    public void Pair_PowerOfTwoHasNoByes()
    {
        var result = WorldCupRules.Pair(["1", "2", "3", "4"]);
        Assert.Empty(result.Byes);
        Assert.Equal(new[] { new KnockoutPair("1", "4"), new KnockoutPair("2", "3") }, result.Matches);
    }
}
