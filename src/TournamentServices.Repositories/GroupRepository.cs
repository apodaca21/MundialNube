using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class GroupRepository(TournamentDbContext db) : IGroupRepository
{
    public async Task<IReadOnlyList<Group>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        return await db.Groups.AsNoTracking()
            .Where(group => group.TournamentId == tournamentId)
            .OrderBy(group => group.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
    {
        return db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(group => group.Id == groupId && group.TournamentId == tournamentId, cancellationToken);
    }

    public Task<Group?> GetByNameAsync(string tournamentId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.ToLower();
        return db.Groups.AsNoTracking()
            .FirstOrDefaultAsync(group =>
                group.TournamentId == tournamentId &&
                group.Name.ToLower() == normalized, cancellationToken);
    }

    public async Task<Group?> FindByTeamIdAsync(string teamId, CancellationToken cancellationToken = default)
    {
        var groups = await db.Groups.AsNoTracking().ToListAsync(cancellationToken);
        return groups.FirstOrDefault(group => group.TeamIds.Contains(teamId));
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Group group, CancellationToken cancellationToken = default)
    {
        var stored = await db.Groups.FindAsync([group.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"Group '{group.Id}' was not found.");

        stored.Rename(group.Name);
        stored.AssignTeams(group.TeamIds.ToList());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var stored = await db.Groups.FindAsync([groupId], cancellationToken);
        if (stored is null)
            return;

        db.Groups.Remove(stored);
        await db.SaveChangesAsync(cancellationToken);
    }
}
