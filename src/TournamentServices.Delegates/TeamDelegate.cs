using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class TeamDelegate : ITeamDelegate
{
    private readonly ITeamRepository _teamRepository;

    public TeamDelegate(ITeamRepository teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
        => _teamRepository.GetAllAsync(cancellationToken);

    public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _teamRepository.GetByIdAsync(id, cancellationToken);

    public async Task<string> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureNameIsUniqueAsync(name, excludedTeamId: null, cancellationToken);

        var team = new Team
        {
            Id = Guid.NewGuid().ToString(),
            Name = name
        };

        await _teamRepository.AddAsync(team, cancellationToken);
        return team.Id;
    }

    public async Task<Team> UpdateAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        var team = await _teamRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new TeamNotFoundException(id);

        await EnsureNameIsUniqueAsync(name, excludedTeamId: id, cancellationToken);

        team.Name = name;
        await _teamRepository.UpdateAsync(team, cancellationToken);
        return team;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = await _teamRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new TeamNotFoundException(id);

        // Referential integrity (groups/matches) will be enforced when those aggregates exist.
        await _teamRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task EnsureNameIsUniqueAsync(
        string name,
        string? excludedTeamId,
        CancellationToken cancellationToken)
    {
        var existing = await _teamRepository.GetByNameAsync(name, cancellationToken);
        if (existing is not null && existing.Id != excludedTeamId)
        {
            throw new DuplicateTeamNameException(name);
        }
    }
}
