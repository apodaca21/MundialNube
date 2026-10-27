using Moq;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class GroupDelegateTests
{
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly GroupDelegate _delegate;

    public GroupDelegateTests()
    {
        _delegate = new GroupDelegate(_groups.Object, _matches.Object, _teams.Object, _tournaments.Object);
    }

    [Fact]
    public async Task GetByTournamentAsync_ReturnsGroups()
    {
        var tournament = SetupTournament();
        var groups = new[] { new Group("group-1", "Group A", tournament.Id) };
        _groups.Setup(r => r.GetByTournamentIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        var result = await _delegate.GetByTournamentAsync(tournament.Id);

        Assert.Same(groups, result);
    }

    [Fact]
    public async Task GetByTournamentAsync_ReturnsEmptyList()
    {
        var tournament = SetupTournament();
        _groups.Setup(r => r.GetByTournamentIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());

        Assert.Empty(await _delegate.GetByTournamentAsync(tournament.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGroup_WhenFound()
    {
        var group = new Group("group-1", "Group A", "cup-1");
        _groups.Setup(r => r.GetByIdAsync("cup-1", "group-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        Assert.Same(group, await _delegate.GetByIdAsync("cup-1", "group-1"));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        Assert.Null(await _delegate.GetByIdAsync("cup-1", "missing"));
    }

    [Fact]
    public async Task CreateAsync_GeneratesId_AndRejectsDuplicateName()
    {
        var tournament = SetupTournament();
        _groups.Setup(r => r.GetByNameAsync(tournament.Id, "Group A", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Group?)null);
        _groups.Setup(r => r.GetByTournamentIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());

        var id = await _delegate.CreateAsync(tournament.Id, "Group A");

        Assert.Matches(Group.IdPattern, id);
        _groups.Verify(r => r.AddAsync(It.Is<Group>(g => g.Name == "Group A"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNameAlreadyExists()
    {
        var tournament = SetupTournament();
        _groups.Setup(r => r.GetByNameAsync(tournament.Id, "Group A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Group("group-1", "Group A", tournament.Id));

        await Assert.ThrowsAsync<DuplicateGroupNameException>(() =>
            _delegate.CreateAsync(tournament.Id, "Group A"));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTournamentIsMissing()
    {
        await Assert.ThrowsAsync<TournamentNotFoundException>(() =>
            _delegate.CreateAsync("missing", "Group A"));
    }

    [Fact]
    public async Task AssignTeamsAsync_SavesValidTeams()
    {
        var tournament = SetupTournament();
        var group = new Group("group-1", "Group A", tournament.Id);
        _groups.Setup(r => r.GetByIdAsync(tournament.Id, group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _teams.Setup(r => r.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });
        _teams.Setup(r => r.GetByIdAsync("team-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-2", Name = "Argentina" });

        await _delegate.AssignTeamsAsync(tournament.Id, group.Id, ["team-1", "team-2"]);

        _groups.Verify(r => r.UpdateAsync(It.Is<Group>(g => g.TeamIds.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignTeamsAsync_Throws_WhenTeamLimitIsExceeded()
    {
        var tournament = SetupTournament();
        var group = new Group("group-1", "Group A", tournament.Id);
        _groups.Setup(r => r.GetByIdAsync(tournament.Id, group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await Assert.ThrowsAsync<GroupRuleViolationException>(() =>
            _delegate.AssignTeamsAsync(tournament.Id, group.Id, ["t1", "t2", "t3", "t4", "t5"]));
    }

    [Fact]
    public async Task AssignTeamsAsync_Throws_WhenTeamIsDuplicated()
    {
        var tournament = SetupTournament();
        var group = new Group("group-1", "Group A", tournament.Id);
        _groups.Setup(r => r.GetByIdAsync(tournament.Id, group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await Assert.ThrowsAsync<GroupRuleViolationException>(() =>
            _delegate.AssignTeamsAsync(tournament.Id, group.Id, ["team-1", "team-1"]));
    }

    [Fact]
    public async Task DeleteAsync_RemovesGroupAndMatches()
    {
        var tournament = SetupTournament();
        var group = new Group("group-1", "Group A", tournament.Id);
        _groups.Setup(r => r.GetByIdAsync(tournament.Id, group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _matches.Setup(r => r.GetByGroupIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Domain.Match("match-1", tournament.Id, group.Id, "t1", "t2")]);

        await _delegate.DeleteAsync(tournament.Id, group.Id);

        _matches.Verify(r => r.DeleteAsync("match-1", It.IsAny<CancellationToken>()), Times.Once);
        _groups.Verify(r => r.DeleteAsync(group.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenGroupIsMissing()
    {
        SetupTournament();

        await Assert.ThrowsAsync<GroupNotFoundException>(() =>
            _delegate.DeleteAsync("cup-1", "missing"));
    }

    private Tournament SetupTournament()
    {
        var tournament = new Tournament
        {
            Id = "cup-1",
            Name = "Mundial 2026",
            Format = new TournamentFormat("FIFA", 8, 4)
        };
        _tournaments.Setup(r => r.GetByIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        return tournament;
    }
}
