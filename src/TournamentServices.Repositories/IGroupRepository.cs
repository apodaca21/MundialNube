using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public interface IGroupRepository
{
    Task<IReadOnlyList<Group>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default);
    Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default);
    Task<Group?> GetByNameAsync(string tournamentId, string name, CancellationToken cancellationToken = default);
    Task AddAsync(Group group, CancellationToken cancellationToken = default);
    Task UpdateAsync(Group group, CancellationToken cancellationToken = default);
    Task DeleteAsync(string groupId, CancellationToken cancellationToken = default);
}