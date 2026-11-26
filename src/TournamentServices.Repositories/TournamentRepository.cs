using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Repositories;

public class TournamentRepository(TournamentDbContext db) : ITournamentRepository
{
    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Tournaments.AsNoTracking().OrderBy(tournament => tournament.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Tournament?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return db.Tournaments.AsNoTracking()
            .FirstOrDefaultAsync(tournament => tournament.Id == id, cancellationToken);
    }

    public async Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        db.Tournaments.Add(tournament);
        var participantCount = checked(tournament.Format.MaxGroups * tournament.Format.MaxTeamsPerGroup);
        db.TournamentTeams.AddRange(Enumerable.Range(1, participantCount).Select(number => new TournamentTeam
        {
            TournamentId = tournament.Id,
            Name = $"Equipo {number:00} - {tournament.Id}"
        }));
        // SaveChanges is transactional: a tournament and its new participants are created together.
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Tournament tournament, CancellationToken cancellationToken = default)
    {
        var stored = await db.Tournaments.FindAsync([tournament.Id], cancellationToken)
            ?? throw new TournamentNotFoundException(tournament.Id);

        stored.Name = tournament.Name;
        stored.Format = tournament.Format;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        // Null-group knockout matches are not removed by the group cascade.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await db.Matches.Where(match => match.TournamentId == id).ExecuteDeleteAsync(cancellationToken);
        // The database removes groups and participants. Historical champions remain.
        var deleted = await db.Tournaments.Where(tournament => tournament.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return deleted > 0;
    }
}
