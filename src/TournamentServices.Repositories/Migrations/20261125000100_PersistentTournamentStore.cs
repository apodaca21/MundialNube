using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentServices.Repositories.Migrations;

/// <summary>Provider-aware baseline: adopts existing SQLite data and creates fresh PostgreSQL stores.</summary>
[DbContext(typeof(TournamentDbContext))]
[Migration("20261125000100_PersistentTournamentStore")]
public sealed class PersistentTournamentStore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(PersistenceSchema.CreateSql(ActiveProvider?.Contains("Npgsql") == true));
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_Matches_TournamentId_Round" ON "Matches" ("TournamentId", "Round");
            CREATE INDEX IF NOT EXISTS "IX_Matches_HomeTeamId" ON "Matches" ("HomeTeamId");
            CREATE INDEX IF NOT EXISTS "IX_Matches_VisitorTeamId" ON "Matches" ("VisitorTeamId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException("The baseline contains user data. Restore a verified backup instead of dropping it.");
}

internal static class PersistenceSchema
{
    internal static string CreateSql(bool postgres)
    {
        var identity = postgres ? "BIGSERIAL PRIMARY KEY" : "INTEGER PRIMARY KEY AUTOINCREMENT";
        var timestamp = postgres ? "timestamp with time zone" : "TEXT";
        var boolean = postgres ? "boolean" : "INTEGER";
        var falseValue = postgres ? "FALSE" : "0";
        return $$"""
            CREATE TABLE IF NOT EXISTS "Tournaments" (
                "Id" TEXT NOT NULL PRIMARY KEY, "Name" TEXT NOT NULL,
                "Format_Type" TEXT NOT NULL, "Format_MaxGroups" INTEGER NOT NULL,
                "Format_MaxTeamsPerGroup" INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS "Groups" (
                "Id" TEXT NOT NULL PRIMARY KEY, "TournamentId" TEXT NOT NULL, "Name" TEXT NOT NULL,
                FOREIGN KEY ("TournamentId") REFERENCES "Tournaments" ("Id") ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS "TournamentTeams" (
                "Id" TEXT NOT NULL PRIMARY KEY, "TournamentId" TEXT NOT NULL, "Name" TEXT NOT NULL,
                FOREIGN KEY ("TournamentId") REFERENCES "Tournaments" ("Id") ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS "ManualTeams" (
                "Id" TEXT NOT NULL PRIMARY KEY, "Name" TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS "GroupTeams" (
                "GroupId" TEXT NOT NULL, "TeamId" TEXT NOT NULL,
                PRIMARY KEY ("GroupId", "TeamId"),
                FOREIGN KEY ("GroupId") REFERENCES "Groups" ("Id") ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS "Matches" (
                "Id" TEXT NOT NULL PRIMARY KEY, "TournamentId" TEXT NOT NULL DEFAULT '', "GroupId" TEXT,
                "HomeTeamId" TEXT NOT NULL DEFAULT '', "VisitorTeamId" TEXT NOT NULL DEFAULT '',
                "HomeTeamScore" INTEGER NOT NULL DEFAULT 0, "VisitorTeamScore" INTEGER NOT NULL DEFAULT 0,
                "IsCompleted" {{boolean}} NOT NULL DEFAULT {{falseValue}}, "Round" INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY ("GroupId") REFERENCES "Groups" ("Id") ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS "TournamentChampions" (
                "Id" {{identity}}, "TournamentId" TEXT NOT NULL, "TournamentName" TEXT NOT NULL,
                "TeamId" TEXT NOT NULL, "TeamName" TEXT NOT NULL, "CompletedAtUtc" {{timestamp}} NOT NULL);
            CREATE TABLE IF NOT EXISTS "OutboxEvents" (
                "Id" TEXT NOT NULL PRIMARY KEY, "Type" TEXT NOT NULL, "Payload" TEXT NOT NULL,
                "OccurredAtUtc" {{timestamp}} NOT NULL, "PublishedAtUtc" {{timestamp}});
            CREATE TABLE IF NOT EXISTS "ConsumedEvents" (
                "Id" TEXT NOT NULL PRIMARY KEY, "Type" TEXT NOT NULL, "ConsumedAtUtc" {{timestamp}} NOT NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ManualTeams_Name" ON "ManualTeams" ("Name");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Groups_TournamentId_Name" ON "Groups" ("TournamentId", "Name");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TournamentTeams_TournamentId_Name" ON "TournamentTeams" ("TournamentId", "Name");
            CREATE INDEX IF NOT EXISTS "IX_GroupTeams_TeamId" ON "GroupTeams" ("TeamId");
            CREATE INDEX IF NOT EXISTS "IX_Matches_GroupId" ON "Matches" ("GroupId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TournamentChampions_TournamentId" ON "TournamentChampions" ("TournamentId");
            CREATE INDEX IF NOT EXISTS "IX_TournamentChampions_CompletedAtUtc_Id" ON "TournamentChampions" ("CompletedAtUtc", "Id");
            CREATE INDEX IF NOT EXISTS "IX_OutboxEvents_PublishedAtUtc_OccurredAtUtc" ON "OutboxEvents" ("PublishedAtUtc", "OccurredAtUtc");
            """;
    }
}
