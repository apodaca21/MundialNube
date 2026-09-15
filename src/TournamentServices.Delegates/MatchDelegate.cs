using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class MatchDelegate(
    IMatchRepository matchRepository,
    IGroupRepository groupRepository,
    ITeamRepository teamRepository,
    ITournamentRepository tournamentRepository) : IMatchDelegate
{
    public async Task<IReadOnlyList<Match>> GetByTournamentAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        return await matchRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
    }

    public Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
        => matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken);

    public async Task<string> CreateAsync(string tournamentId, string? groupId, string homeTeamId, string visitorTeamId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        if (homeTeamId == visitorTeamId)
            throw new MatchRuleViolationException("A match cannot have the same home and visitor team.");

        await EnsureTeamExists(homeTeamId, cancellationToken);
        await EnsureTeamExists(visitorTeamId, cancellationToken);

        if (groupId is not null)
        {
            var group = await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken)
                ?? throw new GroupNotFoundException(groupId);
            if (!group.TeamIds.Contains(homeTeamId) || !group.TeamIds.Contains(visitorTeamId))
                throw new MatchRuleViolationException("Both teams must be assigned to the match group.");
        }

        var match = new Match(Guid.NewGuid().ToString(), tournamentId, groupId, homeTeamId, visitorTeamId);
        await matchRepository.AddAsync(match, cancellationToken);
        return match.Id;
    }

    public async Task<Match> UpdateScoreAsync(string tournamentId, string matchId, Score score,
        CancellationToken cancellationToken = default)
    {
        var match = await matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken)
            ?? throw new MatchNotFoundException(matchId);

        match.UpdateScore(score);
        await matchRepository.UpdateAsync(match, cancellationToken);
        return match;
    }

    public async Task DeleteAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
    {
        if (await matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken) is null)
            throw new MatchNotFoundException(matchId);

        await matchRepository.DeleteAsync(matchId, cancellationToken);
    }

    private async Task EnsureTeamExists(string teamId, CancellationToken cancellationToken)
    {
        if (await teamRepository.GetByIdAsync(teamId, cancellationToken) is null)
            throw new TeamNotFoundException(teamId);
    }

    private async Task<Tournament> EnsureTournamentExists(string tournamentId, CancellationToken cancellationToken)
        => await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);
}