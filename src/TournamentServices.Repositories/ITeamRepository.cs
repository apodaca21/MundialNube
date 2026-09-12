using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    Task UpdateAsync(Team team, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
