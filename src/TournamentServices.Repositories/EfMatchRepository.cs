using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public sealed class EfMatchRepository(TournamentDbContext db) : IMatchRepository
{
    public async Task<IReadOnlyList<Match>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
        => (await db.Matches.AsNoTracking().Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.Round).ThenBy(m => m.Id).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
    {
        var row = await db.Matches.AsNoTracking().FirstOrDefaultAsync(m => m.TournamentId == tournamentId && m.Id == matchId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<Match>> GetByGroupIdAsync(string groupId, CancellationToken cancellationToken = default)
        => (await db.Matches.AsNoTracking().Where(m => m.GroupId == groupId).OrderBy(m => m.Id).ToListAsync(cancellationToken)).Select(Map).ToList();

    public async Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        db.Matches.Add(new TournamentMatch
        {
            Id = match.Id, TournamentId = match.TournamentId, GroupId = match.GroupId,
            HomeTeamId = match.HomeTeamId, VisitorTeamId = match.VisitorTeamId,
            HomeTeamScore = match.Score.HomeTeamScore, VisitorTeamScore = match.Score.VisitorTeamScore,
            IsCompleted = match.IsCompleted, Round = match.Round
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Match match, CancellationToken cancellationToken = default)
    {
        var row = await db.Matches.FindAsync([match.Id], cancellationToken)
            ?? throw new KeyNotFoundException($"Match '{match.Id}' was not found.");
        row.HomeTeamScore = match.Score.HomeTeamScore;
        row.VisitorTeamScore = match.Score.VisitorTeamScore;
        row.IsCompleted = match.IsCompleted;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => await db.Matches.Where(m => m.Id == id).ExecuteDeleteAsync(cancellationToken);

    private static Match Map(TournamentMatch row)
    {
        var match = new Match(row.Id, row.TournamentId, row.GroupId, row.HomeTeamId, row.VisitorTeamId, row.Round);
        if (row.IsCompleted) match.UpdateScore(new Score(row.HomeTeamScore, row.VisitorTeamScore));
        return match;
    }
}
