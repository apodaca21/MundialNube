using System.Collections.Concurrent;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly ConcurrentDictionary<string, Team> _teams = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Team> teams = _teams.Values.Select(Clone).ToList();
        return Task.FromResult(teams);
    }

    public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var team = _teams.TryGetValue(id, out var stored) ? Clone(stored) : null;
        return Task.FromResult(team);
    }

    public Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var stored = _teams.Values.FirstOrDefault(team =>
            team.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(stored is null ? null : Clone(stored));
    }

    public Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        if (!_teams.TryAdd(team.Id, Clone(team)))
        {
            throw new InvalidOperationException($"A team with id '{team.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        if (!_teams.ContainsKey(team.Id))
        {
            throw new KeyNotFoundException($"Team '{team.Id}' was not found.");
        }

        _teams[team.Id] = Clone(team);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _teams.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static Team Clone(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name
    };
}
