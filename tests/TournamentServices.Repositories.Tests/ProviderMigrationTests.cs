using Microsoft.EntityFrameworkCore;
using Npgsql;
using TournamentServices.Domain;

namespace TournamentServices.Repositories.Tests;

public sealed class ProviderMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProviderSnapshotsMatchCurrentModel(bool postgres)
    {
        var options = new DbContextOptionsBuilder<TournamentDbContext>();
        if (postgres) options.UseNpgsql("Host=localhost;Database=unused;Username=unused");
        else options.UseSqlite("Data Source=:memory:");
        using var db = new TournamentDbContext(options.Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [PostgreSqlFact]
    public async Task PostgreSqlMigrationsPersistenceAndOutboxWorkInAnIsolatedSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable("MUNDIAL_TEST_POSTGRES")!;
        var schema = "test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema, Pooling = false };
            var options = new DbContextOptionsBuilder<TournamentDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using (var db = new TournamentDbContext(options))
            {
                await TournamentDatabaseInitializer.InitializeAsync(db);
                await TournamentDatabaseInitializer.InitializeAsync(db);
                Assert.Single(await db.Database.GetAppliedMigrationsAsync());
                await new TournamentRepository(db).AddAsync(new Tournament { Id = "pg-cup", Name = "Postgres cup" });
                var participants = (await new TournamentResultsRepository(db).GetTeamsAsync("pg-cup"))!;
                var group = new Group("pg-group", "A", "pg-cup");
                group.AssignTeams(participants.Take(4).Select(t => t.Id));
                await new EfGroupRepository(db).AddAsync(group);
                var match = new Match("pg-final", "pg-cup", null, participants[0].Id, participants[1].Id, 1);
                match.UpdateScore(new Score(2, 1));
                await new EfMatchRepository(db).AddAsync(match);
                await using var unit = new EfUnitOfWork(db);
                await unit.BeginAsync("pg-cup");
                await new TournamentResultsRepository(db).CompleteAsync("pg-cup", participants[0].Id);
                await unit.CommitAsync();
            }
            await using (var read = new TournamentDbContext(options))
            {
                Assert.Equal(32, await read.TournamentTeams.CountAsync());
                Assert.Equal(4, (await new EfGroupRepository(read).GetByIdAsync("pg-cup", "pg-group"))!.TeamIds.Count);
                Assert.Equal(1, (await new EfMatchRepository(read).GetByIdAsync("pg-cup", "pg-final"))!.Round);
                var result = Assert.Single(await new TournamentResultsRepository(read).GetRecentChampionsAsync());
                Assert.Equal(DateTimeKind.Utc, result.CompletedAtUtc.Kind);
                await new TournamentResultsRepository(read).CompleteAsync("pg-cup", result.TeamId);
                Assert.Single(await read.OutboxEvents.ToListAsync());
                Assert.False(read.Database.HasPendingModelChanges());
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MUNDIAL_TEST_POSTGRES")))
            Skip = "Set MUNDIAL_TEST_POSTGRES to run the isolated PostgreSQL integration test.";
    }
}
