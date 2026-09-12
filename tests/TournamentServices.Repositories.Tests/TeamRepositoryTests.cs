using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Repositories.Tests;

public class TeamRepositoryTests
{
    private readonly TeamRepository _repository = new();

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoTeamsExist()
    {
        var teams = await _repository.GetAllAsync();

        Assert.Empty(teams);
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsCopyOfTeam()
    {
        var team = new Team { Id = "team-1", Name = "Mexico" };

        await _repository.AddAsync(team);
        var stored = await _repository.GetByIdAsync("team-1");

        Assert.NotNull(stored);
        Assert.Equal("team-1", stored.Id);
        Assert.Equal("Mexico", stored.Name);
        Assert.NotSame(team, stored);
    }

    [Fact]
    public async Task UpdateAsync_ChangesName()
    {
        await _repository.AddAsync(new Team { Id = "team-1", Name = "Mexico" });

        await _repository.UpdateAsync(new Team { Id = "team-1", Name = "Argentina" });
        var updated = await _repository.GetByIdAsync("team-1");

        Assert.Equal("Argentina", updated?.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTeam()
    {
        await _repository.AddAsync(new Team { Id = "team-1", Name = "Mexico" });

        await _repository.DeleteAsync("team-1");

        Assert.Null(await _repository.GetByIdAsync("team-1"));
    }
}
