using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public interface ITournamentResultsRepository
{
    Task<TournamentChampion?> GetChampionAsync(string tournamentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentTeam>?> GetTeamsAsync(string tournamentId,
        CancellationToken cancellationToken = default);
    Task<TournamentChampion> CompleteAsync(string tournamentId, string teamId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentChampion>> GetRecentChampionsAsync(int limit = 20,
        CancellationToken cancellationToken = default);
}
