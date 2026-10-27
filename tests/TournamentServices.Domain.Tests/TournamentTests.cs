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
        Assert.Equal(new TournamentFormat(TournamentFormat.Fifa, 8, 4), tournament.Format);
    }

    [Fact]
    public void TournamentBuilder_BuildsTournamentWithChildren()
    {
        var tournament = new TournamentBuilder()
            .WithId("cup-1")
            .WithName("Mundial 2026")
            .WithFormat(new TournamentFormat(TournamentFormat.Fifa, 8, 4))
            .WithGroup(new Group("group-1", "Group A", "cup-1"))
            .Build();

        Assert.Equal("cup-1", tournament.Id);
        Assert.Equal("Mundial 2026", tournament.Name);
        Assert.Single(tournament.Groups);
    }

    [Fact]
    public void TournamentFormat_AcceptsFifa()
    {
        var format = new TournamentFormat(TournamentFormat.Fifa, 8, 4);

        Assert.Equal("FIFA", format.Type);
    }

    [Theory]
    [InlineData(8, 4)]
    public void TournamentFormat_AcceptsValidLimits(int maxGroups, int maxTeamsPerGroup)
    {
        var format = new TournamentFormat(TournamentFormat.Fifa, maxGroups, maxTeamsPerGroup);

        Assert.Equal("FIFA", format.Type);
        Assert.Equal(maxGroups, format.MaxGroups);
        Assert.Equal(maxTeamsPerGroup, format.MaxTeamsPerGroup);
    }

    [Fact]
    public void TournamentFormat_UsesValueEquality()
    {
        var first = new TournamentFormat(TournamentFormat.Fifa, 8, 4);
        var equivalent = new TournamentFormat(TournamentFormat.Fifa, 8, 4);

        Assert.Equal(first, equivalent);
        Assert.Equal(first.GetHashCode(), equivalent.GetHashCode());
        Assert.NotSame(first, equivalent);
   
    }

    [Theory]
    [InlineData("KNOCKOUT")]
    [InlineData("ROUND_ROBIN")]
    [InlineData("NFL")]
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
            () => new TournamentFormat(TournamentFormat.Fifa, maxGroups, 4));

        Assert.Equal("maxGroups", exception.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void TournamentFormat_RejectsInvalidTeamLimits(int maxTeamsPerGroup)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new TournamentFormat(TournamentFormat.Fifa, 8, maxTeamsPerGroup));

        Assert.Equal("maxTeamsPerGroup", exception.ParamName);
    }
}
