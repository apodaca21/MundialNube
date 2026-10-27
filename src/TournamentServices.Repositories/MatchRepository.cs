using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class MatchRepository(TournamentDbContext db) : IMatchRepository
{
    public async Task<IReadOnlyList<Match>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        return await db.Matches.AsNoTracking()
            .Where(match => match.TournamentId == tournamentId)
            .OrderBy(match => match.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
    {
        return db.Matches.AsNoTracking()
            .FirstOrDefaultAsync(match => match.Id == matchId && match.TournamentId == tournamentId, cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetByGroupIdAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return await db.Matches.AsNoTracking()
            .Where(match => match.GroupId == groupId)
            .OrderBy(match => match.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Match>> GetByTeamIdAsync(string teamId, CancellationToken cancellationToken = default)
    {
        return await db.Matches.AsNoTracking()
            .Where(match => match.HomeTeamId == teamId || match.VisitorTeamId == teamId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        db.Matches.Add(match);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Match match, CancellationToken cancellationToken = default)
    {
        var stored = await db.Matches.FindAsync([match.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"Match '{match.Id}' was not found.");

        stored.UpdateScore(match.Score);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var stored = await db.Matches.FindAsync([matchId], cancellationToken);
        if (stored is null)
            return;

        db.Matches.Remove(stored);
        await db.SaveChangesAsync(cancellationToken);
    }
}
