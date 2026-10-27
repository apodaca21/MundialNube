using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Repositories.Tests;

public class TeamRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    private TournamentDbContext CreateContext() => new(
        new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(_connection).Options);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoTeamsExist()
    {
        await using var db = CreateContext();

        Assert.Empty(await new TeamRepository(db).GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsStoredTeam()
    {
        var team = new Team { Id = "team-1", Name = "Mexico" };
        await using (var writeDb = CreateContext())
        {
            await new TeamRepository(writeDb).AddAsync(team);
        }

        await using var readDb = CreateContext();
        var stored = await new TeamRepository(readDb).GetByIdAsync("team-1");

        Assert.NotNull(stored);
        Assert.Equal("team-1", stored.Id);
        Assert.Equal("Mexico", stored.Name);
        Assert.NotSame(team, stored);
    }

    [Fact]
    public async Task UpdateAsync_ChangesName()
    {
        await using (var writeDb = CreateContext())
        {
            await new TeamRepository(writeDb).AddAsync(new Team { Id = "team-1", Name = "Mexico" });
        }

        await using (var updateDb = CreateContext())
        {
            await new TeamRepository(updateDb).UpdateAsync(new Team { Id = "team-1", Name = "Argentina" });
        }

        await using var readDb = CreateContext();
        var updated = await new TeamRepository(readDb).GetByIdAsync("team-1");

        Assert.Equal("Argentina", updated?.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTeam()
    {
        await using (var writeDb = CreateContext())
        {
            await new TeamRepository(writeDb).AddAsync(new Team { Id = "team-1", Name = "Mexico" });
        }

        await using (var deleteDb = CreateContext())
        {
            await new TeamRepository(deleteDb).DeleteAsync("team-1");
        }

        await using var readDb = CreateContext();
        Assert.Null(await new TeamRepository(readDb).GetByIdAsync("team-1"));
    }

    [Fact]
    public async Task GetByNameAsync_IgnoresCase()
    {
        await using (var writeDb = CreateContext())
        {
            await new TeamRepository(writeDb).AddAsync(new Team { Id = "team-1", Name = "Mexico" });
        }

        await using var readDb = CreateContext();
        var stored = await new TeamRepository(readDb).GetByNameAsync("mexico");

        Assert.Equal("team-1", stored?.Id);
    }
}
