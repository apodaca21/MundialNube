using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Repositories.Tests;

public class GroupRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    private TournamentDbContext CreateContext() => new(
        new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(_connection).Options);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        db.Tournaments.Add(new Tournament { Id = "cup-1", Name = "Mundial" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task GetByTournamentIdAsync_ReturnsEmpty_WhenNoGroupsExist()
    {
        await using var db = CreateContext();

        Assert.Empty(await new GroupRepository(db).GetByTournamentIdAsync("cup-1"));
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsGroupWithTeams()
    {
        var group = new Group("group-1", "Group A", "cup-1");
        group.AssignTeams(["team-1", "team-2"]);

        await using (var writeDb = CreateContext())
        {
            await new GroupRepository(writeDb).AddAsync(group);
        }

        await using var readDb = CreateContext();
        var stored = await new GroupRepository(readDb).GetByIdAsync("cup-1", "group-1");

        Assert.NotNull(stored);
        Assert.Equal("Group A", stored.Name);
        Assert.Equal(["team-1", "team-2"], stored.TeamIds);
    }

    [Fact]
    public async Task GetByNameAsync_IgnoresCase()
    {
        await using (var writeDb = CreateContext())
        {
            await new GroupRepository(writeDb).AddAsync(new Group("group-1", "Group A", "cup-1"));
        }

        await using var readDb = CreateContext();
        var stored = await new GroupRepository(readDb).GetByNameAsync("cup-1", "group a");

        Assert.Equal("group-1", stored?.Id);
    }

    [Fact]
    public async Task FindByTeamIdAsync_ReturnsOwner()
    {
        var group = new Group("group-1", "Group A", "cup-1");
        group.AssignTeams(["team-1"]);
        await using (var writeDb = CreateContext())
        {
            await new GroupRepository(writeDb).AddAsync(group);
        }

        await using var readDb = CreateContext();
        var owner = await new GroupRepository(readDb).FindByTeamIdAsync("team-1");

        Assert.Equal("group-1", owner?.Id);
    }

    [Fact]
    public async Task UpdateAsync_RenamesGroup()
    {
        await using (var writeDb = CreateContext())
        {
            await new GroupRepository(writeDb).AddAsync(new Group("group-1", "Group A", "cup-1"));
        }

        await using (var updateDb = CreateContext())
        {
            var group = new Group("group-1", "Group B", "cup-1");
            await new GroupRepository(updateDb).UpdateAsync(group);
        }

        await using var readDb = CreateContext();
        var stored = await new GroupRepository(readDb).GetByIdAsync("cup-1", "group-1");
        Assert.Equal("Group B", stored?.Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGroup()
    {
        await using (var writeDb = CreateContext())
        {
            await new GroupRepository(writeDb).AddAsync(new Group("group-1", "Group A", "cup-1"));
        }

        await using (var deleteDb = CreateContext())
        {
            await new GroupRepository(deleteDb).DeleteAsync("group-1");
        }

        await using var readDb = CreateContext();
        Assert.Null(await new GroupRepository(readDb).GetByIdAsync("cup-1", "group-1"));
    }
}
