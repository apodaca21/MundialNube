using Moq;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class TeamDelegateTests
{
    private readonly Mock<ITeamRepository> _repo = new();
    private readonly TeamDelegate _delegate;

    public TeamDelegateTests()
    {
        _delegate = new TeamDelegate(_repo.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTeam_WhenFound()
    {
        _repo.Setup(r => r.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });

        var result = await _delegate.GetByIdAsync("team-1");

        Assert.NotNull(result);
        Assert.Equal("team-1", result.Id);
        Assert.Equal("Mexico", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var result = await _delegate.GetByIdAsync("missing-team");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTeamsExist()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Team>());

        var result = await _delegate.GetAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTeams_WhenMultipleExist()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Team { Id = "team-1", Name = "Mexico" },
                new Team { Id = "team-2", Name = "Argentina" }
            });

        var result = await _delegate.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Mexico", result[0].Name);
        Assert.Equal("Argentina", result[1].Name);
    }

    [Fact]
    public async Task CreateAsync_GeneratesId_SavesTeam_AndReturnsId()
    {
        Team? saved = null;
        _repo.Setup(r => r.GetByNameAsync("Mexico", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        _repo.Setup(r => r.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Callback<Team, CancellationToken>((team, _) => saved = team)
            .Returns(Task.CompletedTask);

        var id = await _delegate.CreateAsync("Mexico");

        Assert.Matches(Team.IdPattern, id);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal("Mexico", saved.Name);
        _repo.Verify(r => r.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenNameAlreadyExists()
    {
        _repo.Setup(r => r.GetByNameAsync("Mexico", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });

        await Assert.ThrowsAsync<DuplicateTeamNameException>(() => _delegate.CreateAsync("Mexico"));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesName_WhenTeamExists()
    {
        _repo.Setup(r => r.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });
        _repo.Setup(r => r.GetByNameAsync("Argentina", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var updated = await _delegate.UpdateAsync("team-1", "Argentina");

        Assert.Equal("Argentina", updated.Name);
        _repo.Verify(r => r.UpdateAsync(It.Is<Team>(t => t.Name == "Argentina"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenTeamDoesNotExist()
    {
        _repo.Setup(r => r.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<TeamNotFoundException>(() => _delegate.UpdateAsync("missing-team", "Argentina"));
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNameBelongsToAnotherTeam()
    {
        _repo.Setup(r => r.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });
        _repo.Setup(r => r.GetByNameAsync("Argentina", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-2", Name = "Argentina" });

        await Assert.ThrowsAsync<DuplicateTeamNameException>(() => _delegate.UpdateAsync("team-1", "Argentina"));
    }

    [Fact]
    public async Task DeleteAsync_DeletesTeam_WhenFound()
    {
        _repo.Setup(r => r.GetByIdAsync("team-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Team { Id = "team-1", Name = "Mexico" });
        _repo.Setup(r => r.DeleteAsync("team-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _delegate.DeleteAsync("team-1");

        _repo.Verify(r => r.DeleteAsync("team-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Throws_WhenTeamDoesNotExist()
    {
        _repo.Setup(r => r.GetByIdAsync("missing-team", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        await Assert.ThrowsAsync<TeamNotFoundException>(() => _delegate.DeleteAsync("missing-team"));
    }
}
