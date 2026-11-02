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

        if ((await matchRepository.GetByGroupIdAsync(groupId, cancellationToken)).Count > 0)
            throw new GroupRuleViolationException("Cannot rename a group after matches have been created.");

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

        var matches = await matchRepository.GetByGroupIdAsync(groupId, cancellationToken);
        if (matches.Count > 0)
        {
            var current = group.TeamIds.ToHashSet(StringComparer.Ordinal);
            var next = teamIds.ToHashSet(StringComparer.Ordinal);
            if (!current.SetEquals(next))
                throw new GroupRuleViolationException("Cannot change group teams after matches have been created.");
            return;
        }

        if (teamIds.Distinct(StringComparer.Ordinal).Count() != teamIds.Count)
            throw new GroupRuleViolationException("A team cannot be assigned more than once to a group.");

        if (teamIds.Count > tournament.Format.MaxTeamsPerGroup)
            throw new GroupRuleViolationException("The group exceeds the tournament team limit.");

        foreach (var teamId in teamIds)
        {
            if (await teamRepository.GetByIdAsync(teamId, cancellationToken) is null)
                throw new TeamNotFoundException(teamId);

            var owner = await groupRepository.FindByTeamIdAsync(teamId, cancellationToken);
            if (owner is not null && owner.Id != groupId)
                throw new GroupRuleViolationException("A team cannot be assigned to more than one group.");
        }

        group.AssignTeams(teamIds);
        await groupRepository.UpdateAsync(group, cancellationToken);
    }

    public async Task DeleteAsync(string tournamentId, string groupId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        if (await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken) is null)
            throw new GroupNotFoundException(groupId);

        if ((await matchRepository.GetByTournamentAndStageAsync(tournamentId, MatchStages.RoundOf16, cancellationToken)).Count > 0)
            throw new GroupRuleViolationException("Cannot delete a group after the knockout stage has started.");

        var matches = await matchRepository.GetByGroupIdAsync(groupId, cancellationToken);
        foreach (var match in matches)
            await matchRepository.DeleteAsync(match.Id, cancellationToken);

        await groupRepository.DeleteAsync(groupId, cancellationToken);
    }

    private async Task<Tournament> EnsureTournamentExists(string tournamentId, CancellationToken cancellationToken)
        => await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);
}
