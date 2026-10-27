using Moq;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class MatchDelegateTests
{
    private readonly Mock<IMatchRepository> _matches = new();
    private readonly Mock<IGroupRepository> _groups = new();
    private readonly Mock<ITeamRepository> _teams = new();
    private readonly Mock<ITournamentRepository> _tournaments = new();
    private readonly MatchDelegate _delegate;

    public MatchDelegateTests()
    {
        _delegate = new MatchDelegate(_matches.Object, _groups.Object, _teams.Object, _tournaments.Object);
    }

    [Fact]
    public async Task GetByTournamentAsync_ReturnsMatches()
    {
        var tournament = SetupTournament();
        var matches = new[] { new Domain.Match("match-1", tournament.Id, null, "t1", "t2") };
        _matches.Setup(r => r.GetByTournamentIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matches);

        Assert.Same(matches, await _delegate.GetByTournamentAsync(tournament.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMatch_WhenFound()
    {
        var match = new Domain.Match("match-1", "cup-1", null, "t1", "t2");
        _matches.Setup(r => r.GetByIdAsync("cup-1", "match-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        Assert.Same(match, await _delegate.GetByIdAsync("cup-1", "match-1"));
    }

    [Fact]
    public async Task CreateAsync_SavesMatch_WhenTeamsAreInTheGroup()
    {
        var tournament = SetupTournament();
        var group = new Group("group-1", "Group A", tournament.Id);
        group.AssignTeams(["team-1", "team-2"]);
        SetupTeam("team-1");
        SetupTeam("team-2");
        _groups.Setup(r => r.GetByIdAsync(tournament.Id, group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var id = await _delegate.CreateAsync(tournament.Id, group.Id, "team-1", "team-2");

        Assert.Matches(Domain.Match.IdPattern, id);
        _matches.Verify(r => r.AddAsync(It.IsAny<Domain.Match>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTeamsAreTheSame()
    {
        SetupTournament();
        SetupTeam("team-1");

        await Assert.ThrowsAsync<MatchRuleViolationException>(() =>
            _delegate.CreateAsync("cup-1", null, "team-1", "team-1"));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTeamIsMissing()
    {
        SetupTournament();
        SetupTeam("team-1");

        await Assert.ThrowsAsync<TeamNotFoundException>(() =>
            _delegate.CreateAsync("cup-1", null, "team-1", "missing"));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTeamIsNotInTournament()
    {
        var tournament = SetupTournament();
        SetupTeam("team-1");
        SetupTeam("team-2");
        _groups.Setup(r => r.GetByTournamentIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Group>());

        await Assert.ThrowsAsync<MatchRuleViolationException>(() =>
            _delegate.CreateAsync(tournament.Id, null, "team-1", "team-2"));
    }

    [Fact]
    public async Task UpdateScoreAsync_ComputesWinner()
    {
        var match = new Domain.Match("match-1", "cup-1", null, "t1", "t2");
        _matches.Setup(r => r.GetByIdAsync("cup-1", "match-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);

        var updated = await _delegate.UpdateScoreAsync("cup-1", "match-1", new Score(3, 1));

        Assert.Equal(Domain.Enums.Winner.HOME, updated.Winner);
        Assert.True(updated.IsCompleted);
        _matches.Verify(r => r.UpdateAsync(match, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateScoreAsync_Throws_WhenMatchIsMissing()
    {
        await Assert.ThrowsAsync<MatchNotFoundException>(() =>
            _delegate.UpdateScoreAsync("cup-1", "missing", new Score(1, 0)));
    }

    [Fact]
    public async Task DeleteAsync_RemovesMatch()
    {
        _matches.Setup(r => r.GetByIdAsync("cup-1", "match-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Match("match-1", "cup-1", null, "t1", "t2"));

        await _delegate.DeleteAsync("cup-1", "match-1");

        _matches.Verify(r => r.DeleteAsync("match-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenMatchIsMissing()
    {
        await Assert.ThrowsAsync<MatchNotFoundException>(() =>
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

    private void SetupTeam(string id)
    {
        _teams.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = id, Name = id });
    }
}
