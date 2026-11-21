using Microsoft.EntityFrameworkCore;

namespace TournamentServices.Repositories;

/// <summary>Adds the result tables to existing EnsureCreated SQLite databases without losing data.</summary>
public static class TournamentDatabaseInitializer
{
    public static async Task InitializeAsync(TournamentDbContext db,
        CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // EnsureCreated does not add tables when an older database already exists.
        // Keep these additive definitions in sync with TournamentDbContext.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "TournamentTeams" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_TournamentTeams" PRIMARY KEY,
                "TournamentId" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                CONSTRAINT "FK_TournamentTeams_Tournaments_TournamentId"
                    FOREIGN KEY ("TournamentId") REFERENCES "Tournaments" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TournamentTeams_TournamentId_Name"
                ON "TournamentTeams" ("TournamentId", "Name");
            CREATE TABLE IF NOT EXISTS "TournamentChampions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_TournamentChampions" PRIMARY KEY AUTOINCREMENT,
                "TournamentId" TEXT NOT NULL,
                "TournamentName" TEXT NOT NULL,
                "TeamId" TEXT NOT NULL,
                "TeamName" TEXT NOT NULL,
                "CompletedAtUtc" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TournamentChampions_TournamentId"
                ON "TournamentChampions" ("TournamentId");
            CREATE INDEX IF NOT EXISTS "IX_TournamentChampions_CompletedAtUtc_Id"
                ON "TournamentChampions" ("CompletedAtUtc", "Id");
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
