using TournamentServices.Domain;

namespace TournamentServices.Domain.Tests;

public class GroupTests
{
    [Fact]
    public void AssignTeams_ReplacesPreviousList()
    {
        var group = new Group("group-1", "Group A", "cup-1");
        group.AssignTeams(["team-1"]);
        group.AssignTeams(["team-2", "team-3"]);

        Assert.Equal(["team-2", "team-3"], group.TeamIds);
    }

    [Fact]
    public void AssignTeams_KeepsTeams_WhenCalledWithOwnList()
    {
        var group = new Group("group-1", "Group A", "cup-1");
        group.AssignTeams(["team-1", "team-2"]);

        group.AssignTeams(group.TeamIds);

        Assert.Equal(["team-1", "team-2"], group.TeamIds);
    }

    [Fact]
    public void Rename_ChangesName()
    {
        var group = new Group("group-1", "Group A", "cup-1");

        group.Rename("Group B");

        Assert.Equal("Group B", group.Name);
        Assert.Equal("cup-1", group.TournamentId);
    }
}
