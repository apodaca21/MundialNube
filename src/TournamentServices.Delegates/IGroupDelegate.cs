using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface IGroupDelegate
{
    Task<IReadOnlyList<Group>> GetByTournamentAsync(string tournamentId, CancellationToken cancellationToken = default);
    Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(string tournamentId, string name, CancellationToken cancellationToken = default);
    Task<Group> UpdateAsync(string tournamentId, string groupId, string name, CancellationToken cancellationToken = default);
    Task AssignTeamsAsync(string tournamentId, string groupId, IReadOnlyList<string> teamIds, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default);
}