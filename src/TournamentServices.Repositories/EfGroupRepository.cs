using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public sealed class EfGroupRepository(TournamentDbContext db) : IGroupRepository
{
    public async Task<IReadOnlyList<Group>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        var rows = await db.Groups.AsNoTracking().Where(g => g.TournamentId == tournamentId)
            .OrderBy(g => g.Name).ThenBy(g => g.Id).ToListAsync(cancellationToken);
        var ids = rows.Select(g => g.Id).ToArray();
        var memberships = await db.GroupTeams.AsNoTracking().Where(t => ids.Contains(t.GroupId)).ToListAsync(cancellationToken);
        return rows.Select(row => Map(row, memberships.Where(t => t.GroupId == row.Id).Select(t => t.TeamId))).ToList();
    }

    public async Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
    {
        var row = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId && g.TournamentId == tournamentId, cancellationToken);
        if (row is null) return null;
        return Map(row, await db.GroupTeams.Where(t => t.GroupId == row.Id).Select(t => t.TeamId).ToListAsync(cancellationToken));
    }

    public async Task<Group?> GetByNameAsync(string tournamentId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.ToUpperInvariant();
        var row = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.TournamentId == tournamentId && g.Name.ToUpper() == normalized, cancellationToken);
        return row is null ? null : await GetByIdAsync(tournamentId, row.Id, cancellationToken);
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        db.Groups.Add(new TournamentGroup { Id = group.Id, Name = group.Name, TournamentId = group.TournamentId });
        db.GroupTeams.AddRange(group.TeamIds.Select(id => new GroupTeam { GroupId = group.Id, TeamId = id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Group group, CancellationToken cancellationToken = default)
    {
        var row = await db.Groups.FindAsync([group.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"Group '{group.Id}' was not found.");
        row.Name = group.Name;
        var existing = await db.GroupTeams.Where(t => t.GroupId == group.Id).ToListAsync(cancellationToken);
        db.GroupTeams.RemoveRange(existing.Where(t => !group.TeamIds.Contains(t.TeamId)));
        var existingIds = existing.Select(t => t.TeamId).ToHashSet();
        db.GroupTeams.AddRange(group.TeamIds.Where(id => !existingIds.Contains(id)).Select(id => new GroupTeam { GroupId = group.Id, TeamId = id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string groupId, CancellationToken cancellationToken = default)
        => await db.Groups.Where(g => g.Id == groupId).ExecuteDeleteAsync(cancellationToken);

    private static Group Map(TournamentGroup row, IEnumerable<string> ids)
    {
        var group = new Group(row.Id, row.Name, row.TournamentId);
        group.AssignTeams(ids.OrderBy(id => id));
        return group;
    }
}
