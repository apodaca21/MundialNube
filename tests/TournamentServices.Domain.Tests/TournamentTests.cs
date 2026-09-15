using TournamentServices.Domain;

namespace TournamentServices.Domain.Tests;

public class TournamentTests
{
    [Fact]
    public void NewTournament_HasValidIdAndDefaultFormat()
    {
        var tournament = new Tournament();

        Assert.True(Guid.TryParse(tournament.Id, out _));
        Assert.Matches(Tournament.IdPattern, tournament.Id);
        Assert.Equal(new TournamentFormat(TournamentFormat.RoundRobin, 8, 4), tournament.Format);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(8, 4)]
    [InlineData(16, 8)]
    public void TournamentFormat_AcceptsValidLimits(int maxGroups, int maxTeamsPerGroup)
    {
        var format = new TournamentFormat(TournamentFormat.RoundRobin, maxGroups, maxTeamsPerGroup);

        Assert.Equal("ROUND_ROBIN", format.Type);
        Assert.Equal(maxGroups, format.MaxGroups);
        Assert.Equal(maxTeamsPerGroup, format.MaxTeamsPerGroup);
    }

    [Fact]
    public void TournamentFormat_UsesValueEquality()
    {
        var first = new TournamentFormat(TournamentFormat.RoundRobin, 8, 4);
        var equivalent = new TournamentFormat(TournamentFormat.RoundRobin, 8, 4);
        var different = new TournamentFormat(TournamentFormat.RoundRobin, 12, 4);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotSame(first, equivalent);
        Assert.NotEqual(first, different);
    }

    [Theory]
    [InlineData("KNOCKOUT")]
    [InlineData("round_robin")]
    [InlineData("")]
    [InlineData(null)]
    public void TournamentFormat_RejectsUnsupportedTypes(string? type)
    {
        var exception = Assert.Throws<ArgumentException>(() => new TournamentFormat(type!, 8, 4));

        Assert.Equal("type", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void TournamentFormat_RejectsInvalidGroupLimits(int maxGroups)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TournamentFormat(TournamentFormat.RoundRobin, maxGroups, 4));

        Assert.Equal("maxGroups", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void TournamentFormat_RejectsInvalidTeamLimits(int maxTeamsPerGroup)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TournamentFormat(TournamentFormat.RoundRobin, 8, maxTeamsPerGroup));

        Assert.Equal("maxTeamsPerGroup", exception.ParamName);
    }
}
