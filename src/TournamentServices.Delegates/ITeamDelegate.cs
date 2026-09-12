using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface ITeamDelegate
{
    Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<Team> UpdateAsync(string id, string name, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
