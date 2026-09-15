using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default);
    Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Match>> GetByGroupIdAsync(string groupId, CancellationToken cancellationToken = default);
    Task AddAsync(Match match, CancellationToken cancellationToken = default);
    Task UpdateAsync(Match match, CancellationToken cancellationToken = default);
    Task DeleteAsync(string matchId, CancellationToken cancellationToken = default);
}