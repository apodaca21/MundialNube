using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class GroupDelegate(
    IGroupRepository groupRepository,
    IMatchRepository matchRepository,
    ITeamRepository teamRepository,
    ITournamentRepository tournamentRepository) : IGroupDelegate
{
    public async Task<IReadOnlyList<Group>> GetByTournamentAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        return await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
    }

    public Task<Group?> GetByIdAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
        => groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken);

    public async Task<string> CreateAsync(string tournamentId, string name, CancellationToken cancellationToken = default)
    {
        var tournament = await EnsureTournamentExists(tournamentId, cancellationToken);
        if (await groupRepository.GetByNameAsync(tournamentId, name, cancellationToken) is not null)
            throw new DuplicateGroupNameException(name);

        var groups = await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
        if (groups.Count >= tournament.Format.MaxGroups)
            throw new GroupRuleViolationException("The tournament has reached its maximum number of groups.");

        var group = new Group(Guid.NewGuid().ToString(), name, tournamentId);
        await groupRepository.AddAsync(group, cancellationToken);
        return group.Id;
    }

    public async Task<Group> UpdateAsync(string tournamentId, string groupId, string name, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        var group = await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        var existing = await groupRepository.GetByNameAsync(tournamentId, name, cancellationToken);
        if (existing is not null && existing.Id != groupId)
            throw new DuplicateGroupNameException(name);

        group.Rename(name);
        await groupRepository.UpdateAsync(group, cancellationToken);
        return group;
    }

    public async Task AssignTeamsAsync(string tournamentId, string groupId, IReadOnlyList<string> teamIds,
        CancellationToken cancellationToken = default)
    {
        var tournament = await EnsureTournamentExists(tournamentId, cancellationToken);
        var group = await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        if (teamIds.Distinct(StringComparer.Ordinal).Count() != teamIds.Count)
            throw new GroupRuleViolationException("A team cannot be assigned more than once to a group.");

        if (teamIds.Count > tournament.Format.MaxTeamsPerGroup)
            throw new GroupRuleViolationException("The group exceeds the tournament team limit.");

        foreach (var teamId in teamIds)
        {
            if (await teamRepository.GetByIdAsync(teamId, cancellationToken) is null)
                throw new TeamNotFoundException(teamId);
        }

        group.AssignTeams(teamIds);
        await groupRepository.UpdateAsync(group, cancellationToken);
    }

    public async Task DeleteAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        if (await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken) is null)
            throw new GroupNotFoundException(groupId);

        var matches = await matchRepository.GetByGroupIdAsync(groupId, cancellationToken);
        foreach (var match in matches)
            await matchRepository.DeleteAsync(match.Id, cancellationToken);

        await groupRepository.DeleteAsync(groupId, cancellationToken);
    }

    private async Task<Tournament> EnsureTournamentExists(string tournamentId, CancellationToken cancellationToken)
        => await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);
}