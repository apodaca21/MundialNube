using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class TournamentDelegate : ITournamentDelegate
{
    private readonly ITournamentRepository _repo;

    public TournamentDelegate(ITournamentRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repo.GetAllAsync(cancellationToken);

    public Task<Tournament?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public async Task<Tournament> CreateAsync(string name, TournamentFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(format);
        await EnsureUniqueName(name, null, cancellationToken);

        var tournament = new Tournament { Name = name, Format = format };
        await _repo.AddAsync(tournament, cancellationToken);
        return tournament;
    }

    public async Task<Tournament> UpdateAsync(string id, string name, TournamentFormat format,
        CancellationToken cancellationToken = default)
    {
        var tournament = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new TournamentNotFoundException(id);

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(format);
        await EnsureUniqueName(name, id, cancellationToken);

        tournament.Name = name;
        tournament.Format = format;
        await _repo.UpdateAsync(tournament, cancellationToken);
        return tournament;
    }

    public async Task<Tournament> PatchAsync(string id, string? name, string? formatType,
        int? maxGroups, int? maxTeamsPerGroup, CancellationToken cancellationToken = default)
    {
        var tournament = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new TournamentNotFoundException(id);

        var updatedName = name ?? tournament.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedName);
        await EnsureUniqueName(updatedName, id, cancellationToken);

        var updatedFormat = new TournamentFormat(
            formatType ?? tournament.Format.Type,
            maxGroups ?? tournament.Format.MaxGroups,
            maxTeamsPerGroup ?? tournament.Format.MaxTeamsPerGroup);

        tournament.Name = updatedName;
        tournament.Format = updatedFormat;
        await _repo.UpdateAsync(tournament, cancellationToken);
        return tournament;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await _repo.DeleteAsync(id, cancellationToken))
            throw new TournamentNotFoundException(id);
    }

    private async Task EnsureUniqueName(string name, string? currentId, CancellationToken cancellationToken)
    {
        var existing = await _repo.GetByNameAsync(name, cancellationToken);
        if (existing != null && existing.Id != currentId)
            throw new DuplicateTournamentNameException(name);
    }
}
