using Moq;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class TeamDelegateTests
{
    private readonly Mock<ITeamRepository> _repositoryMock;
    private readonly ITeamDelegate _delegate;

    public TeamDelegateTests()
    {
        _repositoryMock = new Mock<ITeamRepository>(MockBehavior.Strict);
        _delegate = new TeamDelegate(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTeam_WhenFound()
    {
        var team = CreateTeam("team-1", "Mexico");
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var result = await _delegate.GetByIdAsync("team-1");

        Assert.NotNull(result);
        Assert.Equal("team-1", result.Id);
        Assert.Equal("Mexico", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var result = await _delegate.GetByIdAsync("missing-team");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTeamsExist()
    {
        _repositoryMock
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Team>());

        var result = await _delegate.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTeams_WhenMultipleExist()
    {
        var teams = new[] { CreateTeam("team-1", "Mexico"), CreateTeam("team-2", "Argentina") };
        _repositoryMock
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(teams);

        var result = await _delegate.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Collection(
            result,
            team => Assert.Equal("Mexico", team.Name),
            team => Assert.Equal("Argentina", team.Name));
    }

    [Fact]
    public async Task CreateAsync_GeneratesId_SavesTeam_AndReturnsId()
    {
        Team? saved = null;
        _repositoryMock
            .Setup(repository => repository.GetByNameAsync("Mexico", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        _repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Callback<Team, CancellationToken>((team, _) => saved = team)
            .Returns(Task.CompletedTask);

        var id = await _delegate.CreateAsync("Mexico");

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Matches(Team.IdPattern, id);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Mexico", saved.Name);
        _repositoryMock.Verify(
            repository => repository.AddAsync(It.Is<Team>(team => team.Id == id && team.Name == "Mexico"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNameAlreadyExists()
    {
        _repositoryMock
            .Setup(repository => repository.GetByNameAsync("Mexico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTeam("team-1", "Mexico"));

        await Assert.ThrowsAsync<DuplicateTeamNameException>(() => _delegate.CreateAsync("Mexico"));
        _repositoryMock.Verify(
            repository => repository.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName_WhenTeamExists()
    {
        var existing = CreateTeam("team-1", "Mexico");
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repositoryMock
            .Setup(repository => repository.GetByNameAsync("Argentina", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        _repositoryMock
            .Setup(repository => repository.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updated = await _delegate.UpdateAsync("team-1", "Argentina");

        Assert.Equal("team-1", updated.Id);
        Assert.Equal("Argentina", updated.Name);
        _repositoryMock.Verify(
            repository => repository.UpdateAsync(It.Is<Team>(team => team.Id == "team-1" && team.Name == "Argentina"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenTeamDoesNotExist()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<TeamNotFoundException>(() => _delegate.UpdateAsync("missing-team", "Argentina"));
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNameBelongsToAnotherTeam()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTeam("team-1", "Mexico"));
        _repositoryMock
            .Setup(repository => repository.GetByNameAsync("Argentina", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTeam("team-2", "Argentina"));

        await Assert.ThrowsAsync<DuplicateTeamNameException>(() => _delegate.UpdateAsync("team-1", "Argentina"));
    }

    [Fact]
    public async Task DeleteAsync_DeletesTeam_WhenFound()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTeam("team-1", "Mexico"));
        _repositoryMock
            .Setup(repository => repository.DeleteAsync("team-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _delegate.DeleteAsync("team-1");

        _repositoryMock.Verify(
            repository => repository.DeleteAsync("team-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenTeamDoesNotExist()
    {
        _repositoryMock
            .Setup(repository => repository.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<TeamNotFoundException>(() => _delegate.DeleteAsync("missing-team"));
        _repositoryMock.Verify(
            repository => repository.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Team CreateTeam(string id, string name) => new()
    {
        Id = id,
        Name = name
    };
}
