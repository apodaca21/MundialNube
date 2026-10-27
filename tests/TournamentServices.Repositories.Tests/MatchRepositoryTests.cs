using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Repositories.Tests;

public class MatchRepositoryTests : IAsyncLifetime
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
        db.Groups.Add(new Group("group-1", "Group A", "cup-1"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task GetByTournamentIdAsync_ReturnsEmpty_WhenNoMatchesExist()
    {
        await using var db = CreateContext();

        Assert.Empty(await new MatchRepository(db).GetByTournamentIdAsync("cup-1"));
    }

    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsMatch()
    {
        var match = new Match("match-1", "cup-1", "group-1", "home-1", "away-1");
        await using (var writeDb = CreateContext())
        {
            await new MatchRepository(writeDb).AddAsync(match);
        }

        await using var readDb = CreateContext();
        var stored = await new MatchRepository(readDb).GetByIdAsync("cup-1", "match-1");

        Assert.NotNull(stored);
        Assert.Equal("home-1", stored.HomeTeamId);
        Assert.False(stored.IsCompleted);
    }

    [Fact]
    public async Task UpdateAsync_PersistsScoreAndWinner()
    {
        await using (var writeDb = CreateContext())
        {
            await new MatchRepository(writeDb).AddAsync(new Match("match-1", "cup-1", "group-1", "home-1", "away-1"));
        }

        await using (var updateDb = CreateContext())
        {
            var match = new Match("match-1", "cup-1", "group-1", "home-1", "away-1");
            match.UpdateScore(new Score(2, 0));
            await new MatchRepository(updateDb).UpdateAsync(match);
        }

        await using var readDb = CreateContext();
        var stored = await new MatchRepository(readDb).GetByIdAsync("cup-1", "match-1");

        Assert.NotNull(stored);
        Assert.True(stored.IsCompleted);
        Assert.Equal(2, stored.Score.HomeTeamScore);
        Assert.Equal(Domain.Enums.Winner.HOME, stored.Winner);
    }

    [Fact]
    public async Task GetByGroupIdAsync_FiltersMatches()
    {
        await using (var writeDb = CreateContext())
        {
            await new MatchRepository(writeDb).AddAsync(new Match("match-1", "cup-1", "group-1", "h1", "a1"));
        }

        await using var readDb = CreateContext();
        var matches = await new MatchRepository(readDb).GetByGroupIdAsync("group-1");

        Assert.Equal("match-1", Assert.Single(matches).Id);
    }

    [Fact]
    public async Task GetByTeamIdAsync_FindsHomeOrVisitor()
    {
        await using (var writeDb = CreateContext())
        {
            await new MatchRepository(writeDb).AddAsync(new Match("match-1", "cup-1", "group-1", "home-1", "away-1"));
        }

        await using var readDb = CreateContext();
        var matches = await new MatchRepository(readDb).GetByTeamIdAsync("away-1");

        Assert.Equal("match-1", Assert.Single(matches).Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesMatch()
    {
        await using (var writeDb = CreateContext())
        {
            await new MatchRepository(writeDb).AddAsync(new Match("match-1", "cup-1", "group-1", "h1", "a1"));
        }

        await using (var deleteDb = CreateContext())
        {
            await new MatchRepository(deleteDb).DeleteAsync("match-1");
        }

        await using var readDb = CreateContext();
        Assert.Null(await new MatchRepository(readDb).GetByIdAsync("cup-1", "match-1"));
    }
}
