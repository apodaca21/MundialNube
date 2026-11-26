using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Repositories;

public sealed class TournamentResultsRepository(TournamentDbContext db) : ITournamentResultsRepository
{
    public Task<TournamentChampion?> GetChampionAsync(string tournamentId, CancellationToken cancellationToken = default)
        => db.TournamentChampions.AsNoTracking().SingleOrDefaultAsync(result => result.TournamentId == tournamentId, cancellationToken);

    public async Task<IReadOnlyList<TournamentTeam>?> GetTeamsAsync(string tournamentId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Tournaments.AnyAsync(tournament => tournament.Id == tournamentId, cancellationToken))
            return null;

        return await db.TournamentTeams.AsNoTracking()
            .Where(team => team.TournamentId == tournamentId)
            .OrderBy(team => team.Name).ThenBy(team => team.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<TournamentChampion> CompleteAsync(string tournamentId, string teamId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Tournaments.AnyAsync(tournament => tournament.Id == tournamentId, cancellationToken))
            throw new TournamentNotFoundException(tournamentId);

        if (!await db.TournamentTeams.AnyAsync(team => team.Id == teamId && team.TournamentId == tournamentId,
                cancellationToken))
            throw new TeamNotInTournamentException(tournamentId, teamId);

        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var completedAtUtc = DateTime.UtcNow;
        // One atomic insert selects the current names and membership from the database.
        // The unique tournament index makes concurrent/repeated requests idempotent.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TournamentChampions"
                ("TournamentId", "TournamentName", "TeamId", "TeamName", "CompletedAtUtc")
            SELECT tournament."Id", tournament."Name", team."Id", team."Name", {completedAtUtc}
            FROM "Tournaments" AS tournament
            INNER JOIN "TournamentTeams" AS team ON team."TournamentId" = tournament."Id"
            WHERE tournament."Id" = {tournamentId} AND team."Id" = {teamId}
            ON CONFLICT ("TournamentId") DO NOTHING;
            """, cancellationToken);

        var champion = await db.TournamentChampions.AsNoTracking()
            .SingleOrDefaultAsync(result => result.TournamentId == tournamentId, cancellationToken);
        if (champion is null)
        {
            // The tournament or participant may have been deleted between validation and the insert.
            if (!await db.Tournaments.AnyAsync(tournament => tournament.Id == tournamentId, cancellationToken))
                throw new TournamentNotFoundException(tournamentId);
            throw new TeamNotInTournamentException(tournamentId, teamId);
        }
        if (champion.TeamId != teamId)
            throw new TournamentAlreadyCompletedException(tournamentId);
        if (inserted > 0)
        {
            db.OutboxEvents.Add(new OutboxEvent
            {
                Id = $"champion-{champion.Id}", Type = "champion.completed", OccurredAtUtc = champion.CompletedAtUtc,
                Payload = JsonSerializer.Serialize(new
                {
                    source = "mundialnube", id = champion.Id, tournamentId = champion.TournamentId,
                    tournamentName = champion.TournamentName, teamId = champion.TeamId,
                    teamName = champion.TeamName, completedAtUtc = champion.CompletedAtUtc
                })
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return champion;
    }

    public async Task<IReadOnlyList<TournamentChampion>> GetRecentChampionsAsync(int limit = 20,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 20);
        return await db.TournamentChampions.AsNoTracking()
            .OrderByDescending(champion => champion.CompletedAtUtc)
            .ThenByDescending(champion => champion.Id)
            .Take(limit).ToListAsync(cancellationToken);
    }
}
