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
    private const int GroupCount = 8;
    private const int TeamsPerGroup = 4;

    public async Task<IReadOnlyList<Match>> GetByTournamentAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        return await matchRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
    }

    public Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
        => matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken);

    public async Task<IReadOnlyList<GroupStanding>> GetStandingsAsync(string tournamentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        var groups = OrderGroups(await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken));
        var matches = await matchRepository.GetByTournamentAndStageAsync(
            tournamentId, MatchStages.Group, cancellationToken);
        var names = (await teamRepository.GetAllAsync(cancellationToken))
            .ToDictionary(team => team.Id, team => team.Name, StringComparer.Ordinal);
        return GroupStandingsCalculator.Calculate(groups, matches, names);
    }

    public async Task<IReadOnlyList<Match>> GenerateGroupStageAsync(string tournamentId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        if (await HasStage(tournamentId, MatchStages.RoundOf16, cancellationToken))
            throw new MatchRuleViolationException("The group stage is closed because the knockout stage has started.");

        var groups = await GetReadyGroups(tournamentId, cancellationToken);
        var existing = await matchRepository.GetByTournamentAndStageAsync(
            tournamentId, MatchStages.Group, cancellationToken);
        var existingPairs = existing
            .Where(match => match.GroupId is not null)
            .Select(GroupPairKey)
            .ToHashSet(StringComparer.Ordinal);
        var created = new List<Match>();

        foreach (var group in groups)
        {
            for (var home = 0; home < group.TeamIds.Count; home++)
            {
                for (var visitor = home + 1; visitor < group.TeamIds.Count; visitor++)
                {
                    var key = GroupPairKey(group.Id, group.TeamIds[home], group.TeamIds[visitor]);
                    if (existingPairs.Contains(key))
                        continue;

                    created.Add(new Match(Guid.NewGuid().ToString(), tournamentId, group.Id,
                        group.TeamIds[home], group.TeamIds[visitor]));
                    existingPairs.Add(key);
                }
            }
        }

        await matchRepository.AddRangeAsync(created, cancellationToken);
        return await matchRepository.GetByTournamentAndStageAsync(
            tournamentId, MatchStages.Group, cancellationToken);
    }

    public async Task<string> CreateAsync(string tournamentId, string? groupId, string homeTeamId, string visitorTeamId,
        CancellationToken cancellationToken = default)
    {
        await EnsureTournamentExists(tournamentId, cancellationToken);
        if (string.IsNullOrWhiteSpace(groupId))
            throw new MatchRuleViolationException("A group is required to create a match.");
        if (homeTeamId == visitorTeamId)
            throw new MatchRuleViolationException("A match cannot have the same home and visitor team.");

        await EnsureTeamExists(homeTeamId, cancellationToken);
        await EnsureTeamExists(visitorTeamId, cancellationToken);
        await EnsureStageIsOpen(tournamentId, MatchStages.Group, cancellationToken);

        var group = await groupRepository.GetByIdAsync(tournamentId, groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);
        if (!group.TeamIds.Contains(homeTeamId) || !group.TeamIds.Contains(visitorTeamId))
            throw new MatchRuleViolationException("Both teams must be assigned to the match group.");

        var existing = await matchRepository.GetByTournamentAndStageAsync(
            tournamentId, MatchStages.Group, cancellationToken);
        if (existing.Any(match => match.GroupId == groupId &&
                                  SamePair(match, homeTeamId, visitorTeamId)))
            throw new MatchRuleViolationException("A match between these teams already exists in the group stage.");

        var match = new Match(Guid.NewGuid().ToString(), tournamentId, groupId, homeTeamId, visitorTeamId);
        await matchRepository.AddAsync(match, cancellationToken);
        return match.Id;
    }

    public async Task<Match> UpdateScoreAsync(string tournamentId, string matchId, Score score,
        CancellationToken cancellationToken = default)
    {
        var match = await matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken)
            ?? throw new MatchNotFoundException(matchId);

        await EnsureStageIsOpen(tournamentId, match.Stage, cancellationToken);
        match.UpdateScore(score);
        await matchRepository.UpdateAsync(match, cancellationToken);
        await TryAdvanceAsync(tournamentId, match.Stage, cancellationToken);
        return match;
    }

    public async Task DeleteAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
    {
        var match = await matchRepository.GetByIdAsync(tournamentId, matchId, cancellationToken)
            ?? throw new MatchNotFoundException(matchId);

        await EnsureStageIsOpen(tournamentId, match.Stage, cancellationToken);
        await matchRepository.DeleteAsync(matchId, cancellationToken);
    }

    private async Task EnsureTeamExists(string teamId, CancellationToken cancellationToken)
    {
        if (await teamRepository.GetByIdAsync(teamId, cancellationToken) is null)
            throw new TeamNotFoundException(teamId);
    }

    private async Task<IReadOnlyList<Group>> GetReadyGroups(string tournamentId,
        CancellationToken cancellationToken)
    {
        var groups = OrderGroups(await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken));
        if (!GroupsAreReady(groups))
            throw new MatchRuleViolationException(
                "The tournament needs exactly 8 groups with 4 distinct teams to generate the group stage.");

        return groups;
    }

    private async Task EnsureStageIsOpen(string tournamentId, string stage, CancellationToken cancellationToken)
    {
        foreach (var nextStage in MatchStages.NextStages(stage))
        {
            if (await HasStage(tournamentId, nextStage, cancellationToken))
                throw new MatchRuleViolationException(
                    $"The {stage} stage is closed because the next stage has already started.");
        }
    }

    private async Task TryAdvanceAsync(string tournamentId, string completedStage,
        CancellationToken cancellationToken)
    {
        if (completedStage == MatchStages.Group)
        {
            await TryGenerateRoundOf16Async(tournamentId, cancellationToken);
            return;
        }

        if (completedStage is MatchStages.RoundOf16 or MatchStages.Quarterfinal or MatchStages.Semifinal)
            await TryGenerateNextKnockoutRoundAsync(tournamentId, completedStage, cancellationToken);
    }

    private async Task TryGenerateRoundOf16Async(string tournamentId, CancellationToken cancellationToken)
    {
        if (await HasStage(tournamentId, MatchStages.RoundOf16, cancellationToken))
            return;

        var groups = OrderGroups(await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken));
        if (!GroupsAreReady(groups))
            return;

        var groupMatches = await matchRepository.GetByTournamentAndStageAsync(
            tournamentId, MatchStages.Group, cancellationToken);
        foreach (var group in groups)
        {
            for (var first = 0; first < group.TeamIds.Count; first++)
            {
                for (var second = first + 1; second < group.TeamIds.Count; second++)
                {
                    var matches = groupMatches.Where(match =>
                        match.GroupId == group.Id &&
                        SamePair(match, group.TeamIds[first], group.TeamIds[second])).ToList();
                    if (matches.Count != 1 || !matches[0].IsCompleted)
                        return;
                }
            }
        }

        var names = (await teamRepository.GetAllAsync(cancellationToken))
            .ToDictionary(team => team.Id, team => team.Name, StringComparer.Ordinal);
        var standings = GroupStandingsCalculator.Calculate(groups, groupMatches, names);
        var firstPlaces = new GroupStanding[GroupCount];
        var secondPlaces = new GroupStanding[GroupCount];
        for (var index = 0; index < groups.Count; index++)
        {
            var table = standings.Where(row => row.GroupId == groups[index].Id)
                .OrderBy(row => row.Rank)
                .ToList();
            firstPlaces[index] = table[0];
            secondPlaces[index] = table[1];
        }

        var pairings = new[]
        {
            (firstPlaces[0].TeamId, secondPlaces[1].TeamId),
            (firstPlaces[2].TeamId, secondPlaces[3].TeamId),
            (firstPlaces[4].TeamId, secondPlaces[5].TeamId),
            (firstPlaces[6].TeamId, secondPlaces[7].TeamId),
            (firstPlaces[1].TeamId, secondPlaces[0].TeamId),
            (firstPlaces[3].TeamId, secondPlaces[2].TeamId),
            (firstPlaces[5].TeamId, secondPlaces[4].TeamId),
            (firstPlaces[7].TeamId, secondPlaces[6].TeamId)
        };
        var matchesToCreate = pairings.Select((pairing, index) =>
            NewKnockout(tournamentId, pairing.Item1, pairing.Item2, MatchStages.RoundOf16, index + 1)).ToList();
        await matchRepository.AddRangeAsync(matchesToCreate, cancellationToken);
    }

    private async Task TryGenerateNextKnockoutRoundAsync(string tournamentId, string completedStage,
        CancellationToken cancellationToken)
    {
        var expectedCount = completedStage switch
        {
            MatchStages.RoundOf16 => 8,
            MatchStages.Quarterfinal => 4,
            MatchStages.Semifinal => 2,
            _ => 0
        };
        var previous = (await matchRepository.GetByTournamentAndStageAsync(
                tournamentId, completedStage, cancellationToken))
            .OrderBy(match => match.RoundPosition)
            .ToList();
        if (previous.Count != expectedCount || previous.Any(match => !match.IsCompleted || match.Winner is null))
            return;

        if (completedStage == MatchStages.Semifinal)
        {
            if (await HasStage(tournamentId, MatchStages.Final, cancellationToken) ||
                await HasStage(tournamentId, MatchStages.ThirdPlace, cancellationToken))
                return;

            await matchRepository.AddRangeAsync(
            [
                NewKnockout(tournamentId, previous[0].LoserTeamId, previous[1].LoserTeamId,
                    MatchStages.ThirdPlace, 1),
                NewKnockout(tournamentId, previous[0].WinnerTeamId, previous[1].WinnerTeamId,
                    MatchStages.Final, 1)
            ], cancellationToken);
            return;
        }

        var nextStage = completedStage == MatchStages.RoundOf16
            ? MatchStages.Quarterfinal
            : MatchStages.Semifinal;
        if (await HasStage(tournamentId, nextStage, cancellationToken))
            return;

        var matchesToCreate = new List<Match>();
        for (var index = 0; index < previous.Count; index += 2)
        {
            matchesToCreate.Add(NewKnockout(tournamentId,
                previous[index].WinnerTeamId,
                previous[index + 1].WinnerTeamId,
                nextStage,
                (index / 2) + 1));
        }

        await matchRepository.AddRangeAsync(matchesToCreate, cancellationToken);
    }

    private async Task<bool> HasStage(string tournamentId, string stage, CancellationToken cancellationToken)
        => (await matchRepository.GetByTournamentAndStageAsync(tournamentId, stage, cancellationToken)).Count > 0;

    private static Match NewKnockout(string tournamentId, string homeTeamId, string visitorTeamId,
        string stage, int position)
        => new(Guid.NewGuid().ToString(), tournamentId, null, homeTeamId, visitorTeamId, stage, position);

    private static bool SamePair(Match match, string firstTeamId, string secondTeamId)
        => match.HomeTeamId == firstTeamId && match.VisitorTeamId == secondTeamId ||
           match.HomeTeamId == secondTeamId && match.VisitorTeamId == firstTeamId;

    private static string GroupPairKey(Match match)
        => GroupPairKey(match.GroupId!, match.HomeTeamId, match.VisitorTeamId);

    private static string GroupPairKey(string groupId, string firstTeamId, string secondTeamId)
    {
        var (first, second) = string.CompareOrdinal(firstTeamId, secondTeamId) <= 0
            ? (firstTeamId, secondTeamId)
            : (secondTeamId, firstTeamId);
        return $"{groupId}:{first}:{second}";
    }

    private static bool GroupsAreReady(IReadOnlyCollection<Group> groups)
        => groups.Count == GroupCount && groups.All(group =>
            group.TeamIds.Distinct(StringComparer.Ordinal).Count() == TeamsPerGroup);

    private static List<Group> OrderGroups(IEnumerable<Group> groups)
        => groups
            .OrderBy(group => FifaGroupIndex(group.Name))
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static int FifaGroupIndex(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return int.MaxValue;

        var letter = char.ToUpperInvariant(trimmed[^1]);
        return letter is >= 'A' and <= 'H' ? letter - 'A' : int.MaxValue;
    }

    private async Task<Tournament> EnsureTournamentExists(string tournamentId, CancellationToken cancellationToken)
        => await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);
}
