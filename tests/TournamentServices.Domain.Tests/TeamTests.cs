using TournamentServices.Domain;

namespace TournamentServices.Domain.Tests;

public class TeamTests
{
    [Fact]
    public void NewTeam_GeneratesId_MatchingRequiredPattern()
    {
        var team = new Team();

        Assert.False(string.IsNullOrWhiteSpace(team.Id));
        Assert.Matches(Team.IdPattern, team.Id);
        Assert.True(Guid.TryParse(team.Id, out _));
    }

    [Theory]
    [InlineData("team-1")]
    [InlineData("ABC123")]
    [InlineData("a")]
    public void IdPattern_AcceptsAlphanumericAndHyphen(string id)
    {
        Assert.Matches(Team.IdPattern, id);
    }

    [Theory]
    [InlineData("invalid id")]
    [InlineData("team_1")]
    [InlineData("id!")]
    [InlineData("")]
    public void IdPattern_RejectsInvalidValues(string id)
    {
        Assert.DoesNotMatch(Team.IdPattern, id);
    }
}
