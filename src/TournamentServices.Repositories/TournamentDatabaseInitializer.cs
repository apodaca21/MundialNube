using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Repositories.Migrations;

namespace TournamentServices.Repositories;

/// <summary>Explicit migration entry point. Production replicas never invoke this at startup.</summary>
public static class TournamentDatabaseInitializer
{
    public static async Task InitializeAsync(TournamentDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            // Adopts pre-v3 EnsureCreated databases; a new SQLite database needs no adoption.
            await db.Database.OpenConnectionAsync(cancellationToken);
            var connection = db.Database.GetDbConnection();
            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Tournaments'";
            var legacy = Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) > 0;
            if (legacy)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                await db.Database.ExecuteSqlRawAsync(PersistenceSchema.CreateSql(false), cancellationToken);
                await UpgradeLegacyMatchesAsync(db, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task UpgradeLegacyMatchesAsync(TournamentDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "PRAGMA table_info('Matches')";
        var columns = new HashSet<string>();
        var requiredGroup = false;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
                if (reader.GetString(1) == "GroupId") requiredGroup = reader.GetInt32(3) == 1;
            }
        }
        var additions = new Dictionary<string, string>
        {
            ["TournamentId"] = "TEXT NOT NULL DEFAULT ''", ["HomeTeamId"] = "TEXT NOT NULL DEFAULT ''",
            ["VisitorTeamId"] = "TEXT NOT NULL DEFAULT ''", ["HomeTeamScore"] = "INTEGER NOT NULL DEFAULT 0",
            ["VisitorTeamScore"] = "INTEGER NOT NULL DEFAULT 0", ["IsCompleted"] = "INTEGER NOT NULL DEFAULT 0",
            ["Round"] = "INTEGER NOT NULL DEFAULT 0"
        };
        // The only interpolated tokens below are identifiers/types from this fixed internal map.
#pragma warning disable EF1002
        foreach (var (name, type) in additions)
            if (!columns.Contains(name))
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"Matches\" ADD COLUMN \"{name}\" {type}", cancellationToken);
#pragma warning restore EF1002
        if (requiredGroup)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE "Matches_v3" (
                    "Id" TEXT NOT NULL PRIMARY KEY, "TournamentId" TEXT NOT NULL, "GroupId" TEXT,
                    "HomeTeamId" TEXT NOT NULL, "VisitorTeamId" TEXT NOT NULL,
                    "HomeTeamScore" INTEGER NOT NULL, "VisitorTeamScore" INTEGER NOT NULL,
                    "IsCompleted" INTEGER NOT NULL, "Round" INTEGER NOT NULL,
                    FOREIGN KEY ("GroupId") REFERENCES "Groups" ("Id") ON DELETE CASCADE);
                INSERT INTO "Matches_v3" SELECT "Id", "TournamentId", "GroupId", "HomeTeamId", "VisitorTeamId",
                    "HomeTeamScore", "VisitorTeamScore", "IsCompleted", "Round" FROM "Matches";
                DROP TABLE "Matches";
                ALTER TABLE "Matches_v3" RENAME TO "Matches";
                """, cancellationToken);
        }
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Matches_GroupId" ON "Matches" ("GroupId");
            CREATE INDEX IF NOT EXISTS "IX_Matches_TournamentId_Round" ON "Matches" ("TournamentId", "Round");
            CREATE INDEX IF NOT EXISTS "IX_Matches_HomeTeamId" ON "Matches" ("HomeTeamId");
            CREATE INDEX IF NOT EXISTS "IX_Matches_VisitorTeamId" ON "Matches" ("VisitorTeamId");
            """, cancellationToken);
    }
}
