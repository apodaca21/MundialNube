using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public interface ITournamentRepository
{
    Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tournament?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(Tournament tournament, CancellationToken cancellationToken = default);
    Task UpdateAsync(Tournament tournament, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
