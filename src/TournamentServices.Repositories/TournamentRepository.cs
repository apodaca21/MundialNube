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
        // SQLite elimina también los grupos y sus partidos mediante las claves foráneas.
        var deleted = await db.Tournaments.Where(tournament => tournament.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }
}
