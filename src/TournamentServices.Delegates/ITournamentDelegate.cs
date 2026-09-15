using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface ITournamentDelegate
{
    Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tournament?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Tournament> CreateAsync(string name, TournamentFormat format, CancellationToken cancellationToken = default);
    Task<Tournament> UpdateAsync(string id, string name, TournamentFormat format, CancellationToken cancellationToken = default);
    Task<Tournament> PatchAsync(string id, string? name, string? formatType, int? maxGroups,
        int? maxTeamsPerGroup, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
