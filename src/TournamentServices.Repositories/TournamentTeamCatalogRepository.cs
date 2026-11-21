using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

/// <summary>Makes generated teams usable through the existing team, group and match APIs.</summary>
public class TournamentTeamCatalogRepository(TournamentDbContext db, TeamRepository manualTeams) : ITeamRepository
{
    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var generated = await db.Set<TournamentTeam>().AsNoTracking()
            .Select(team => new Team { Id = team.Id, Name = team.Name, TournamentId = team.TournamentId }).ToListAsync(cancellationToken);
        generated.AddRange(await manualTeams.GetAllAsync(cancellationToken));
        return generated.OrderBy(team => team.Name).ThenBy(team => team.Id).ToList();
    }

    public async Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => await db.Set<TournamentTeam>().AsNoTracking().Where(team => team.Id == id)
            .Select(team => new Team { Id = team.Id, Name = team.Name, TournamentId = team.TournamentId }).FirstOrDefaultAsync(cancellationToken)
            ?? await manualTeams.GetByIdAsync(id, cancellationToken);

    public async Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => await db.Set<TournamentTeam>().AsNoTracking()
            .Where(team => EF.Functions.Collate(team.Name, "NOCASE") == name)
            .Select(team => new Team { Id = team.Id, Name = team.Name, TournamentId = team.TournamentId }).FirstOrDefaultAsync(cancellationToken)
            ?? await manualTeams.GetByNameAsync(name, cancellationToken);

    public Task AddAsync(Team team, CancellationToken cancellationToken = default)
        => manualTeams.AddAsync(team, cancellationToken);

    public async Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        var generated = await db.Set<TournamentTeam>().FindAsync([team.Id], cancellationToken);
        if (generated is null)
        {
            await manualTeams.UpdateAsync(team, cancellationToken);
            return;
        }
        generated.Name = team.Name;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await db.Set<TournamentTeam>().Where(team => team.Id == id).ExecuteDeleteAsync(cancellationToken);
        await manualTeams.DeleteAsync(id, cancellationToken);
    }
}
