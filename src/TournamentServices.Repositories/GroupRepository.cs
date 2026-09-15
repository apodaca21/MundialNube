using System.Collections.Concurrent;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly ConcurrentDictionary<string, Group> _groups = new();

    public Task<IReadOnlyList<Group>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Group>>(_groups.Values
            .Where(group => group.TournamentId == tournamentId)
            .OrderBy(group => group.Id)
            .Select(Copy)
            .ToList());

    public Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
        => Task.FromResult(_groups.TryGetValue(groupId, out var group) && group.TournamentId == tournamentId
            ? Copy(group)
            : null);

    public Task<Group?> GetByNameAsync(string tournamentId, string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_groups.Values.FirstOrDefault(group =>
            group.TournamentId == tournamentId &&
            group.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } group
                ? Copy(group)
                : null);

    public Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        if (!_groups.TryAdd(group.Id, Copy(group)))
            throw new InvalidOperationException($"A group with id '{group.Id}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Group group, CancellationToken cancellationToken = default)
    {
        if (!_groups.ContainsKey(group.Id))
            throw new KeyNotFoundException($"Group '{group.Id}' was not found.");

        _groups[group.Id] = Copy(group);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        _groups.TryRemove(groupId, out _);
        return Task.CompletedTask;
    }

    private static Group Copy(Group group)
    {
        var copy = new Group(group.Id, group.Name, group.TournamentId);
        copy.AssignTeams(group.TeamIds);
        return copy;
    }
}