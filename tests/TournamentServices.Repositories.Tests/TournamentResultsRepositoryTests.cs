using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Repositories.Tests;

public class TournamentResultsRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private TournamentDbContext CreateContext() => new(
        new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(_connection).Options);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await TournamentDatabaseInitializer.InitializeAsync(db);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task NewTournaments_Have32DistinctParticipants_ThatSurviveNewContext()
    {
        await using (var db = CreateContext())
        {
            var repository = new TournamentRepository(db);
            await repository.AddAsync(new Tournament { Id = "one", Name = "One" });
            await repository.AddAsync(new Tournament { Id = "two", Name = "Two" });
        }

        await using var readDb = CreateContext();
        var results = new TournamentResultsRepository(readDb);
        var first = await results.GetTeamsAsync("one");
        var second = await results.GetTeamsAsync("two");
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(32, first.Count);
        Assert.Equal(32, second.Count);
        Assert.Equal(32, first.Select(team => team.Name).Distinct().Count());
        Assert.Equal(64, first.Concat(second).Select(team => team.Id).Distinct().Count());
        Assert.Equal(64, first.Concat(second).Select(team => team.Name).Distinct().Count());
        Assert.All(first, team => Assert.Equal("one", team.TournamentId));
        Assert.All(second, team => Assert.Equal("two", team.TournamentId));
        Assert.Empty(readDb.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetTeamsAsync_ReturnsNullForMissingTournament_AndEmptyForLegacyTournament()
    {
        await using var db = CreateContext();
        db.Tournaments.Add(new Tournament { Id = "legacy", Name = "Legacy" });
        await db.SaveChangesAsync();
        var repository = new TournamentResultsRepository(db);
        Assert.Null(await repository.GetTeamsAsync("missing"));
        Assert.Empty((await repository.GetTeamsAsync("legacy"))!);
    }

    [Fact]
    public async Task CompleteAsync_PersistsSnapshotWithUtcTime_AndDoesNotDuplicateOnRetry()
    {
        var before = DateTime.UtcNow;
        TournamentChampion original;
        await using (var db = CreateContext())
        {
            await new TournamentRepository(db).AddAsync(new Tournament { Id = "cup", Name = "Original cup" });
            var repository = new TournamentResultsRepository(db);
            var team = (await repository.GetTeamsAsync("cup"))![0];
            original = await repository.CompleteAsync("cup", team.Id);
            var repeat = await repository.CompleteAsync("cup", team.Id);
            Assert.Equal(original.Id, repeat.Id);
            Assert.Equal(original.CompletedAtUtc, repeat.CompletedAtUtc);
            Assert.Equal("Original cup", original.TournamentName);
            Assert.Equal(team.Name, original.TeamName);
        }

        await using var readDb = CreateContext();
        var stored = Assert.Single(await new TournamentResultsRepository(readDb).GetRecentChampionsAsync());
        Assert.Equal(original.Id, stored.Id);
        Assert.InRange(stored.CompletedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, stored.CompletedAtUtc.Kind);
    }

    [Fact]
    public async Task CompleteAsync_RejectsMissingTournamentAndForeignParticipant()
    {
        await using var db = CreateContext();
        var tournaments = new TournamentRepository(db);
        await tournaments.AddAsync(new Tournament { Id = "one", Name = "One" });
        await tournaments.AddAsync(new Tournament { Id = "two", Name = "Two" });
        var repository = new TournamentResultsRepository(db);
        var otherTeam = (await repository.GetTeamsAsync("two"))![0];
        await Assert.ThrowsAsync<TournamentNotFoundException>(() => repository.CompleteAsync("missing", otherTeam.Id));
        await Assert.ThrowsAsync<TeamNotInTournamentException>(() => repository.CompleteAsync("one", otherTeam.Id));
        await Assert.ThrowsAsync<TeamNotInTournamentException>(() => repository.CompleteAsync("one", "missing"));
        Assert.Empty(await repository.GetRecentChampionsAsync());
    }

    [Fact]
    public async Task CompleteAsync_RejectsChangingAnExistingChampion()
    {
        await using var db = CreateContext();
        await new TournamentRepository(db).AddAsync(new Tournament { Id = "cup", Name = "Cup" });
        var repository = new TournamentResultsRepository(db);
        var teams = (await repository.GetTeamsAsync("cup"))!;
        var original = await repository.CompleteAsync("cup", teams[0].Id);
        await Assert.ThrowsAsync<TournamentAlreadyCompletedException>(() => repository.CompleteAsync("cup", teams[1].Id));
        Assert.Equal(original.TeamId, Assert.Single(await repository.GetRecentChampionsAsync()).TeamId);
    }

    [Fact]
    public async Task HistoricalSnapshot_SurvivesRenamingAndDeletingTournament()
    {
        TournamentChampion champion;
        await using (var db = CreateContext())
        {
            var tournaments = new TournamentRepository(db);
            await tournaments.AddAsync(new Tournament { Id = "cup", Name = "Original cup" });
            var results = new TournamentResultsRepository(db);
            var team = (await results.GetTeamsAsync("cup"))![0];
            champion = await results.CompleteAsync("cup", team.Id);
            await tournaments.UpdateAsync(new Tournament { Id = "cup", Name = "Renamed cup" });
            Assert.Equal("Original cup", Assert.Single(await results.GetRecentChampionsAsync()).TournamentName);
            Assert.True(await tournaments.DeleteAsync("cup"));
        }

        await using var readDb = CreateContext();
        Assert.Empty(await readDb.TournamentTeams.ToListAsync());
        var historical = Assert.Single(await new TournamentResultsRepository(readDb).GetRecentChampionsAsync());
        Assert.Equal(champion.Id, historical.Id);
        Assert.Equal("Original cup", historical.TournamentName);
        Assert.Equal(champion.TeamName, historical.TeamName);
    }

    [Fact]
    public async Task RecentChampions_LimitsInDatabaseAndOrdersByCompletionThenSequence()
    {
        await using (var db = CreateContext())
        {
            // Seed controlled completion dates to prove date ordering independent of insertion order.
            for (var number = 24; number >= 0; number--)
                db.TournamentChampions.Add(new TournamentChampion
                {
                    TournamentId = $"cup-{number}", TournamentName = $"Cup {number}",
                    TeamId = $"team-{number}", TeamName = $"Team {number}",
                    CompletedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(number)
                });
            await db.SaveChangesAsync();
        }
        await using var readDb = CreateContext();
        var repository = new TournamentResultsRepository(readDb);
        var recent = await repository.GetRecentChampionsAsync();
        Assert.Equal(20, recent.Count);
        Assert.Equal(Enumerable.Range(5, 20).Reverse().Select(number => $"cup-{number}"), recent.Select(row => row.TournamentId));
        Assert.Equal("cup-24", Assert.Single(await repository.GetRecentChampionsAsync(1)).TournamentId);
        Assert.Empty(readDb.ChangeTracker.Entries());
    }

    [Fact]
    public async Task RecentChampions_BreaksTimestampTiesBySequence()
    {
        await using var db = CreateContext();
        var time = DateTime.UtcNow;
        db.TournamentChampions.Add(new TournamentChampion { TournamentId = "first", CompletedAtUtc = time });
        await db.SaveChangesAsync();
        db.TournamentChampions.Add(new TournamentChampion { TournamentId = "second", CompletedAtUtc = time });
        await db.SaveChangesAsync();
        Assert.Equal("second", Assert.Single(await new TournamentResultsRepository(db).GetRecentChampionsAsync(1)).TournamentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public async Task RecentChampions_RejectsUnsupportedLimits(int limit)
    {
        await using var db = CreateContext();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new TournamentResultsRepository(db).GetRecentChampionsAsync(limit));
    }

    [Fact]
    public async Task Initializer_UpgradesLegacyDatabaseIdempotentlyWithoutLosingData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(connection).Options;
        await using var db = new TournamentDbContext(options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Tournaments" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "Format_Type" TEXT NOT NULL,
                "Format_MaxGroups" INTEGER NOT NULL,
                "Format_MaxTeamsPerGroup" INTEGER NOT NULL
            );
            INSERT INTO "Tournaments" VALUES ('legacy', 'Original data', 'ROUND_ROBIN', 8, 4);
            """);
        await TournamentDatabaseInitializer.InitializeAsync(db);
        await TournamentDatabaseInitializer.InitializeAsync(db);
        Assert.Equal("Original data", (await db.Tournaments.SingleAsync()).Name);
        Assert.Empty(await db.TournamentChampions.ToListAsync());
        var tournaments = new TournamentRepository(db);
        await tournaments.AddAsync(new Tournament { Id = "new", Name = "New cup" });
        var results = new TournamentResultsRepository(db);
        var team = (await results.GetTeamsAsync("new"))![0];
        await results.CompleteAsync("new", team.Id);
        await TournamentDatabaseInitializer.InitializeAsync(db);
        Assert.Single(await results.GetRecentChampionsAsync());
        Assert.Equal(32, (await results.GetTeamsAsync("new"))!.Count);
    }
}
