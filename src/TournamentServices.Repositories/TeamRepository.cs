using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TeamRepository(TournamentDbContext db) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Teams.AsNoTracking()
            .OrderBy(team => team.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(team => team.Id == id, cancellationToken);
    }

    public Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.ToLower();
        return db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(team => team.Name.ToLower() == normalized, cancellationToken);
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        var stored = await db.Teams.FindAsync([team.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"Team '{team.Id}' was not found.");

        stored.Name = team.Name;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var stored = await db.Teams.FindAsync([id], cancellationToken);
        if (stored is null)
            return;

        db.Teams.Remove(stored);
        await db.SaveChangesAsync(cancellationToken);
    }
}
