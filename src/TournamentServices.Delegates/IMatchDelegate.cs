using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface IMatchDelegate
{
    Task<IReadOnlyList<Match>> GetByTournamentAsync(string tournamentId, CancellationToken cancellationToken = default);
    Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(string tournamentId, string? groupId, string homeTeamId, string visitorTeamId,
        CancellationToken cancellationToken = default);
    Task<Match> UpdateScoreAsync(string tournamentId, string matchId, Score score,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default);
}