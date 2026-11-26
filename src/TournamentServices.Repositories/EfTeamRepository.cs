using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

/// <summary>Persistent manual catalog and tournament-specific participants exposed through one API.</summary>
public sealed class EfTeamRepository(TournamentDbContext db) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var teams = await db.ManualTeams.AsNoTracking().ToListAsync(cancellationToken);
        teams.AddRange(await db.TournamentTeams.AsNoTracking()
            .Select(t => new Team { Id = t.Id, Name = t.Name, TournamentId = t.TournamentId }).ToListAsync(cancellationToken));
        return teams.OrderBy(t => t.Name).ThenBy(t => t.Id).ToList();
    }

    public async Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.TournamentTeams.AsNoTracking().Where(t => t.Id == id)
            .Select(t => new Team { Id = t.Id, Name = t.Name, TournamentId = t.TournamentId }).FirstOrDefaultAsync(cancellationToken)
            ?? await db.ManualTeams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.ToUpperInvariant();
        return await db.TournamentTeams.AsNoTracking().Where(t => t.Name.ToUpper() == normalized)
            .Select(t => new Team { Id = t.Id, Name = t.Name, TournamentId = t.TournamentId }).FirstOrDefaultAsync(cancellationToken)
            ?? await db.ManualTeams.AsNoTracking().FirstOrDefaultAsync(t => t.Name.ToUpper() == normalized, cancellationToken);
    }

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        db.ManualTeams.Add(new Team { Id = team.Id, Name = team.Name });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        var generated = await db.TournamentTeams.FindAsync([team.Id], cancellationToken);
        if (generated is not null) generated.Name = team.Name;
        else
        {
            var manual = await db.ManualTeams.FindAsync([team.Id], cancellationToken)
                ?? throw new KeyNotFoundException($"Team '{team.Id}' was not found.");
            manual.Name = team.Name;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        // Membership has two possible sources (manual or generated), so clean references explicitly.
        await db.GroupTeams.Where(t => t.TeamId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Matches.Where(m => m.HomeTeamId == id || m.VisitorTeamId == id).ExecuteDeleteAsync(cancellationToken);
        await db.TournamentTeams.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
        await db.ManualTeams.Where(t => t.Id == id).ExecuteDeleteAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
}
