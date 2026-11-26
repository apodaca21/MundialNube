using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories.Tests;

public sealed class PersistentStoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private TournamentDbContext Context() => new(new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(_connection).Options);
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = Context();
        await TournamentDatabaseInitializer.InitializeAsync(db);
    }
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task OperationalEntitiesPersistAcrossContextsIncludingDrawAndRound()
    {
        await using (var db = Context())
        {
            await new TournamentRepository(db).AddAsync(new Tournament { Id = "cup", Name = "Cup" });
            var teams = new EfTeamRepository(db);
            await teams.AddAsync(new Team { Id = "manual", Name = "Manual" });
            var generated = (await new TournamentResultsRepository(db).GetTeamsAsync("cup"))![0];
            var group = new Group("group", "A", "cup");
            group.AssignTeams(["manual", generated.Id]);
            await new EfGroupRepository(db).AddAsync(group);
            var match = new Match("match", "cup", "group", "manual", generated.Id);
            match.UpdateScore(new Score(2, 2));
            await new EfMatchRepository(db).AddAsync(match);
            var knockout = new Match("final", "cup", null, "manual", generated.Id, 1);
            knockout.UpdateScore(new Score(1, 0));
            await new EfMatchRepository(db).AddAsync(knockout);
        }
        await using var read = Context();
        Assert.Equal("Manual", (await new EfTeamRepository(read).GetByIdAsync("manual"))!.Name);
        Assert.Equal(33, (await new EfTeamRepository(read).GetAllAsync()).Count);
        Assert.Equal(2, (await new EfGroupRepository(read).GetByIdAsync("cup", "group"))!.TeamIds.Count);
        var matchRead = (await new EfMatchRepository(read).GetByIdAsync("cup", "match"))!;
        Assert.True(matchRead.IsCompleted);
        Assert.Null(matchRead.Winner);
        Assert.Equal(2, matchRead.Score.HomeTeamScore);
        var final = (await new EfMatchRepository(read).GetByIdAsync("cup", "final"))!;
        Assert.Equal(1, final.Round);
        Assert.Null(final.GroupId);
    }

    [Fact]
    public async Task UnitOfWorkRollsBackWithoutCommit_AndCommitsExplicitly()
    {
        await using var db = Context();
        await using (var unit = new EfUnitOfWork(db))
        {
            await unit.BeginAsync();
            await new TournamentRepository(db).AddAsync(new Tournament { Id = "rollback", Name = "Rollback" });
        }
        Assert.Empty(await db.Tournaments.ToListAsync());
        Assert.Empty(await db.TournamentTeams.ToListAsync());
        await using (var unit = new EfUnitOfWork(db))
        {
            await unit.BeginAsync();
            await new TournamentRepository(db).AddAsync(new Tournament { Id = "commit", Name = "Commit" });
            await unit.CommitAsync();
        }
        Assert.Single(await db.Tournaments.ToListAsync());
        Assert.Equal(32, await db.TournamentTeams.CountAsync());
    }

    [Fact]
    public async Task ChampionAndEventAreAtomicAndRepeatedCompletionDoesNotDuplicateEvent()
    {
        await using var db = Context();
        await new TournamentRepository(db).AddAsync(new Tournament { Id = "cup", Name = "Cup" });
        var results = new TournamentResultsRepository(db);
        var team = (await results.GetTeamsAsync("cup"))![0];
        await using (var unit = new EfUnitOfWork(db))
        {
            await unit.BeginAsync("cup");
            await results.CompleteAsync("cup", team.Id);
        }
        Assert.Empty(await db.OutboxEvents.ToListAsync());
        Assert.Null(await results.GetChampionAsync("cup"));
        var champion = await results.CompleteAsync("cup", team.Id);
        await results.CompleteAsync("cup", team.Id);
        var message = await db.OutboxEvents.SingleAsync();
        Assert.Equal($"champion-{champion.Id}", message.Id);
        Assert.Equal("champion.completed", message.Type);
        Assert.Contains("\"tournamentId\":\"cup\"", message.Payload);
        Assert.Contains($"\"teamId\":\"{team.Id}\"", message.Payload);
    }

    [Fact]
    public async Task DeletingTournamentAlsoRemovesKnockoutMatches()
    {
        await using var db = Context();
        var tournaments = new TournamentRepository(db);
        await tournaments.AddAsync(new Tournament { Id = "cup", Name = "Cup" });
        var teams = (await new TournamentResultsRepository(db).GetTeamsAsync("cup"))!;
        await new EfMatchRepository(db).AddAsync(new Match("final", "cup", null, teams[0].Id, teams[1].Id, 1));
        await tournaments.DeleteAsync("cup");
        Assert.Empty(await db.Matches.ToListAsync());
    }

    [Fact]
    public async Task MigrationAdoptsActualLegacyTablesAndKeepsMatchesAndChampions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TournamentDbContext(new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Tournaments" ("Id" TEXT PRIMARY KEY, "Name" TEXT NOT NULL,
                "Format_Type" TEXT NOT NULL, "Format_MaxGroups" INTEGER NOT NULL, "Format_MaxTeamsPerGroup" INTEGER NOT NULL);
            CREATE TABLE "Groups" ("Id" TEXT PRIMARY KEY, "TournamentId" TEXT NOT NULL, "Name" TEXT NOT NULL,
                FOREIGN KEY ("TournamentId") REFERENCES "Tournaments"("Id") ON DELETE CASCADE);
            CREATE TABLE "Matches" ("Id" TEXT PRIMARY KEY, "GroupId" TEXT NOT NULL,
                FOREIGN KEY ("GroupId") REFERENCES "Groups"("Id") ON DELETE CASCADE);
            CREATE TABLE "TournamentChampions" ("Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                "TournamentId" TEXT NOT NULL, "TournamentName" TEXT NOT NULL, "TeamId" TEXT NOT NULL,
                "TeamName" TEXT NOT NULL, "CompletedAtUtc" TEXT NOT NULL);
            INSERT INTO "Tournaments" VALUES('legacy','Legacy','ROUND_ROBIN',8,4);
            INSERT INTO "Groups" VALUES('group','legacy','A');
            INSERT INTO "Matches" VALUES('legacy-match','group');
            INSERT INTO "TournamentChampions" VALUES(1,'legacy','Legacy','old-team','Old team','2026-11-20 12:00:00');
            """);
        await TournamentDatabaseInitializer.InitializeAsync(db);
        await TournamentDatabaseInitializer.InitializeAsync(db);
        Assert.Single(await db.TournamentChampions.ToListAsync());
        Assert.Single(await db.Matches.ToListAsync());
        Assert.Equal("legacy-match", (await db.Matches.SingleAsync()).Id);
        Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        // Nullable GroupId now allows a knockout row without disturbing the legacy match.
        await new EfMatchRepository(db).AddAsync(new Match("final", "legacy", null, "one", "two", 1));
        Assert.Equal(2, await db.Matches.CountAsync());
    }
}
