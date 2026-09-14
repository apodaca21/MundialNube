using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Repositories.Tests;

public class TournamentRepositoryTests : IAsyncLifetime
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
    public async Task GetAllAsync_ReturnsEmpty_WhenNoTournamentsExist()
    {
        await using var db = CreateContext();
        var repository = new TournamentRepository(db);

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task AddAsync_PersistsTournamentAndFormat_BetweenContexts()
    {
        var tournament = new Tournament
        {
            Id = "world-cup",
            Name = "Mundial 2026",
            Format = new TournamentFormat(TournamentFormat.RoundRobin, 12, 4)
        };
        await using (var writeDb = CreateContext())
        {
            await new TournamentRepository(writeDb).AddAsync(tournament);
        }

        await using var readDb = CreateContext();
        var repository = new TournamentRepository(readDb);
        var stored = await repository.GetByIdAsync(tournament.Id);

        Assert.NotNull(stored);
        Assert.Equal(tournament.Name, stored.Name);
        Assert.Equal(tournament.Format, stored.Format);
        Assert.NotSame(tournament, stored);
        Assert.Equal(tournament.Id, Assert.Single(await repository.GetAllAsync()).Id);
        Assert.Empty(readDb.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenTournamentDoesNotExist()
    {
        await using var db = CreateContext();

        Assert.Null(await new TournamentRepository(db).GetByIdAsync("missing"));
    }

    [Fact]
    public async Task UpdateAsync_SavesNameAndFormat()
    {
        await using (var writeDb = CreateContext())
        {
            var repository = new TournamentRepository(writeDb);
            await repository.AddAsync(new Tournament { Id = "cup", Name = "Original" });
        }

        await using (var updateDb = CreateContext())
        {
            await new TournamentRepository(updateDb).UpdateAsync(new Tournament
            {
                Id = "cup",
                Name = "Updated",
                Format = new TournamentFormat(TournamentFormat.RoundRobin, 12, 6)
            });
        }

        await using var readDb = CreateContext();
        var updated = await new TournamentRepository(readDb).GetByIdAsync("cup");

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Name);
        Assert.Equal(new TournamentFormat(TournamentFormat.RoundRobin, 12, 6), updated.Format);
    }

    [Fact]
    public async Task UpdateAsync_Works_WhenTournamentIsAlreadyTracked()
    {
        await using var db = CreateContext();
        var repository = new TournamentRepository(db);
        await repository.AddAsync(new Tournament { Id = "cup", Name = "Original" });

        await repository.UpdateAsync(new Tournament { Id = "cup", Name = "Updated" });

        Assert.Equal("Updated", (await repository.GetByIdAsync("cup"))?.Name);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenTournamentDoesNotExist()
    {
        await using var db = CreateContext();
        var repository = new TournamentRepository(db);

        await Assert.ThrowsAsync<TournamentNotFoundException>(() =>
            repository.UpdateAsync(new Tournament { Id = "missing", Name = "Updated" }));
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenTournamentDoesNotExist()
    {
        await using var db = CreateContext();

        Assert.False(await new TournamentRepository(db).DeleteAsync("missing"));
    }

    [Fact]
    public async Task DeleteAsync_CascadesToGroupsAndMatches_AndPreservesOtherTournaments()
    {
        await using (var seedDb = CreateContext())
        {
            seedDb.Tournaments.AddRange(
                new Tournament { Id = "delete-cup", Name = "Delete" },
                new Tournament { Id = "keep-cup", Name = "Keep" });
            seedDb.Groups.AddRange(
                new TournamentGroup { Id = "delete-a", TournamentId = "delete-cup", Name = "A" },
                new TournamentGroup { Id = "delete-b", TournamentId = "delete-cup", Name = "B" },
                new TournamentGroup { Id = "keep-a", TournamentId = "keep-cup", Name = "A" });
            seedDb.Matches.AddRange(
                new TournamentMatch { Id = "delete-match-1", GroupId = "delete-a" },
                new TournamentMatch { Id = "delete-match-2", GroupId = "delete-a" },
                new TournamentMatch { Id = "delete-match-3", GroupId = "delete-b" },
                new TournamentMatch { Id = "keep-match", GroupId = "keep-a" });
            await seedDb.SaveChangesAsync();
        }

        // Un contexto nuevo no carga dependencias: la cascada la ejecuta SQLite.
        await using (var deleteDb = CreateContext())
        {
            Assert.True(await new TournamentRepository(deleteDb).DeleteAsync("delete-cup"));
        }

        await using var readDb = CreateContext();
        Assert.Equal("keep-cup", (await readDb.Tournaments.SingleAsync()).Id);
        Assert.Equal("keep-a", (await readDb.Groups.SingleAsync()).Id);
        Assert.Equal("keep-match", (await readDb.Matches.SingleAsync()).Id);
    }

    [Fact]
    public async Task SaveChangesAsync_RejectsGroupWithoutTournament()
    {
        await using var db = CreateContext();
        db.Groups.Add(new TournamentGroup { TournamentId = "missing", Name = "A" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_RejectsMatchWithoutGroup()
    {
        await using var db = CreateContext();
        db.Matches.Add(new TournamentMatch { GroupId = "missing" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
