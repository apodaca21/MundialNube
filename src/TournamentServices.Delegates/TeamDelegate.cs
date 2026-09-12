using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class TeamDelegate : ITeamDelegate
{
    private readonly ITeamRepository _repo;

    public TeamDelegate(ITeamRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repo.GetAllAsync(cancellationToken);

    public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public async Task<string> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureUniqueName(name, null, cancellationToken);

        var team = new Team
        {
            Id = Guid.NewGuid().ToString(),
            Name = name
        };

        await _repo.AddAsync(team, cancellationToken);
        return team.Id;
    }

    public async Task<Team> UpdateAsync(string id, string name, CancellationToken cancellationToken = default)
    {
        var team = await _repo.GetByIdAsync(id, cancellationToken);
        if (team == null)
            throw new TeamNotFoundException(id);

        await EnsureUniqueName(name, id, cancellationToken);

        team.Name = name;
        await _repo.UpdateAsync(team, cancellationToken);
        return team;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var team = await _repo.GetByIdAsync(id, cancellationToken);
        if (team == null)
            throw new TeamNotFoundException(id);

        await _repo.DeleteAsync(id, cancellationToken);
    }

    private async Task EnsureUniqueName(string name, string? currentId, CancellationToken cancellationToken)
    {
        var existing = await _repo.GetByNameAsync(name, cancellationToken);
        if (existing != null && existing.Id != currentId)
            throw new DuplicateTeamNameException(name);
    }
}
