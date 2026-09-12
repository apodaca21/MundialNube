using System.Collections.Concurrent;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly ConcurrentDictionary<string, Team> _teams = new();

    public Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Team>>(_teams.Values.Select(Copy).ToList());
    }

    public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_teams.TryGetValue(id, out var team) ? Copy(team) : null);
    }

    public Task<Team?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var team = _teams.Values.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(team == null ? null : Copy(team));
    }

    public Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        if (!_teams.TryAdd(team.Id, Copy(team)))
            throw new InvalidOperationException($"A team with id '{team.Id}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Team team, CancellationToken cancellationToken = default)
    {
        if (!_teams.ContainsKey(team.Id))
            throw new KeyNotFoundException($"Team '{team.Id}' was not found.");

        _teams[team.Id] = Copy(team);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _teams.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    private static Team Copy(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name
    };
}
