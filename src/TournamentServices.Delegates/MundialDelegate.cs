using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public class MundialDelegate(
    ITournamentRepository tournamentRepository,
    ITeamRepository teamRepository,
    IGroupRepository groupRepository,
    IMatchRepository matchRepository) : IMundialDelegate
{
    private static readonly string[] GroupNames =
    [
        "Grupo A", "Grupo B", "Grupo C", "Grupo D",
        "Grupo E", "Grupo F", "Grupo G", "Grupo H"
    ];

    private static readonly string[] CountryNames =
    [
        "Mexico", "Argentina", "Brasil", "Francia",
        "Alemania", "Espana", "Inglaterra", "Italia",
        "Paises Bajos", "Portugal", "Uruguay", "Belgica",
        "Croacia", "Estados Unidos", "Japon", "Corea del Sur",
        "Australia", "Marruecos", "Senegal", "Ghana",
        "Nigeria", "Colombia", "Ecuador", "Chile",
        "Suiza", "Polonia", "Dinamarca", "Suecia",
        "Serbia", "Iran", "Canada", "Qatar"
    ];

    public async Task<MundialSimulation> SimulateAsync(string tournamentId, CancellationToken cancellationToken = default)
    {
        var tournament = await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken)
            ?? throw new TournamentNotFoundException(tournamentId);

        var groups = await EnsureGroupsAsync(tournament, cancellationToken);
        await FillGroupsAsync(groups, cancellationToken);
        groups = (await groupRepository.GetByTournamentIdAsync(tournamentId, cancellationToken))
            .OrderBy(group => group.Name)
            .Take(8)
            .ToList();

        var previous = await matchRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
        foreach (var match in previous)
            await matchRepository.DeleteAsync(match.Id, cancellationToken);

        var random = new Random();
        var names = (await teamRepository.GetAllAsync(cancellationToken))
            .ToDictionary(team => team.Id, team => team.Name, StringComparer.Ordinal);

        var groupMatches = new List<Match>();
        foreach (var group in groups)
        {
            var teamIds = group.TeamIds.Take(4).ToList();
            for (var i = 0; i < teamIds.Count; i++)
            {
                for (var j = i + 1; j < teamIds.Count; j++)
                {
                    var match = Play(tournamentId, group.Id, teamIds[i], teamIds[j], MatchStages.Group, random, true);
                    await matchRepository.AddAsync(match, cancellationToken);
                    groupMatches.Add(match);
                }
            }
        }

        var standings = RankGroups(groups, groupMatches, names);
        var first = new GroupStanding[8];
        var second = new GroupStanding[8];
        if (groups.Count < 8 || standings.Count < 32)
            throw new InvalidOperationException("The tournament needs 8 groups of 4 teams to build a Mundial bracket.");

        for (var i = 0; i < 8; i++)
        {
            var table = standings.Where(row => row.GroupId == groups[i].Id).OrderBy(row => row.Rank).ToList();
            if (table.Count < 2)
                throw new InvalidOperationException($"Group '{groups[i].Name}' does not have enough teams for knockout.");
            first[i] = table[0];
            second[i] = table[1];
        }

        var roundOf16 = new List<Match>
        {
            await AddKnockout(tournamentId, first[0], second[1], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[2], second[3], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[4], second[5], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[6], second[7], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[1], second[0], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[3], second[2], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[5], second[4], MatchStages.RoundOf16, random, cancellationToken),
            await AddKnockout(tournamentId, first[7], second[6], MatchStages.RoundOf16, random, cancellationToken)
        };

        var quarters = await PlayWinners(tournamentId, roundOf16, MatchStages.Quarterfinal, random, cancellationToken);
        var semis = await PlayWinners(tournamentId, quarters, MatchStages.Semifinal, random, cancellationToken);

        var thirdPlace = Play(tournamentId, null, semis[0].LoserTeamId, semis[1].LoserTeamId,
            MatchStages.ThirdPlace, random, false);
        await matchRepository.AddAsync(thirdPlace, cancellationToken);

        var final = Play(tournamentId, null, semis[0].WinnerTeamId, semis[1].WinnerTeamId,
            MatchStages.Final, random, false);
        await matchRepository.AddAsync(final, cancellationToken);

        var matches = await matchRepository.GetByTournamentIdAsync(tournamentId, cancellationToken);
        var refreshed = await tournamentRepository.GetByIdAsync(tournamentId, cancellationToken);

        return new MundialSimulation
        {
            Tournament = refreshed!,
            ChampionTeamId = final.WinnerTeamId,
            ChampionName = names[final.WinnerTeamId],
            RunnerUpName = names[final.LoserTeamId],
            ThirdPlaceName = names[thirdPlace.WinnerTeamId],
            Standings = standings,
            Matches = matches
        };
    }

    private async Task<List<Group>> EnsureGroupsAsync(Tournament tournament, CancellationToken cancellationToken)
    {
        var groups = (await groupRepository.GetByTournamentIdAsync(tournament.Id, cancellationToken)).ToList();
        foreach (var name in GroupNames)
        {
            if (groups.Count >= tournament.Format.MaxGroups)
                break;
            if (groups.Any(group => group.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var group = new Group(Guid.NewGuid().ToString(), name, tournament.Id);
            await groupRepository.AddAsync(group, cancellationToken);
            groups.Add(group);
        }

        return groups.OrderBy(group => group.Name).Take(8).ToList();
    }

    private async Task FillGroupsAsync(IReadOnlyList<Group> groups, CancellationToken cancellationToken)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            foreach (var teamId in group.TeamIds)
                used.Add(teamId);
        }

        foreach (var group in groups)
        {
            var teamIds = group.TeamIds.Distinct(StringComparer.Ordinal).ToList();
            while (teamIds.Count < 4)
            {
                var team = await NextFreeTeamAsync(used, cancellationToken);
                teamIds.Add(team.Id);
                used.Add(team.Id);
            }

            group.AssignTeams(teamIds.Take(4).ToList());
            await groupRepository.UpdateAsync(group, cancellationToken);
        }
    }

    private async Task<Team> NextFreeTeamAsync(HashSet<string> used, CancellationToken cancellationToken)
    {
        var teams = await teamRepository.GetAllAsync(cancellationToken);
        foreach (var team in teams)
        {
            if (used.Contains(team.Id))
                continue;
            if (await groupRepository.FindByTeamIdAsync(team.Id, cancellationToken) is not null)
                continue;
            return team;
        }

        foreach (var name in CountryNames)
        {
            var existing = await teamRepository.GetByNameAsync(name, cancellationToken);
            if (existing is null)
                return await CreateTeamAsync(name, cancellationToken);
            if (!used.Contains(existing.Id) &&
                await groupRepository.FindByTeamIdAsync(existing.Id, cancellationToken) is null)
                return existing;
        }

        return await CreateTeamAsync("Seleccion " + Guid.NewGuid().ToString("N")[..8], cancellationToken);
    }

    private async Task<Team> CreateTeamAsync(string name, CancellationToken cancellationToken)
    {
        var team = new Team { Id = Guid.NewGuid().ToString(), Name = name };
        await teamRepository.AddAsync(team, cancellationToken);
        return team;
    }

    private async Task<Match> AddKnockout(string tournamentId, GroupStanding home, GroupStanding visitor,
        string stage, Random random, CancellationToken cancellationToken)
    {
        var match = Play(tournamentId, null, home.TeamId, visitor.TeamId, stage, random, false);
        await matchRepository.AddAsync(match, cancellationToken);
        return match;
    }

    private async Task<List<Match>> PlayWinners(string tournamentId, IReadOnlyList<Match> previous, string stage,
        Random random, CancellationToken cancellationToken)
    {
        var next = new List<Match>();
        for (var i = 0; i < previous.Count; i += 2)
        {
            var match = Play(tournamentId, null, previous[i].WinnerTeamId, previous[i + 1].WinnerTeamId,
                stage, random, false);
            await matchRepository.AddAsync(match, cancellationToken);
            next.Add(match);
        }

        return next;
    }

    private static Match Play(string tournamentId, string? groupId, string homeTeamId, string visitorTeamId,
        string stage, Random random, bool allowDraw)
    {
        var home = random.Next(0, 5);
        var visitor = random.Next(0, 5);
        if (!allowDraw && home == visitor)
            home++;

        var match = new Match(Guid.NewGuid().ToString(), tournamentId, groupId, homeTeamId, visitorTeamId, stage);
        match.UpdateScore(new Score(home, visitor));
        return match;
    }

    private static IReadOnlyList<GroupStanding> RankGroups(IReadOnlyList<Group> groups, IReadOnlyList<Match> matches,
        IReadOnlyDictionary<string, string> names)
    {
        var result = new List<GroupStanding>();
        foreach (var group in groups)
        {
            var rows = group.TeamIds.Select(teamId => new GroupStanding
            {
                GroupId = group.Id,
                GroupName = group.Name,
                TeamId = teamId,
                TeamName = names.TryGetValue(teamId, out var name) ? name : teamId
            }).ToDictionary(row => row.TeamId, StringComparer.Ordinal);

            foreach (var match in matches.Where(item => item.GroupId == group.Id && item.IsCompleted))
            {
                Apply(rows[match.HomeTeamId], match.Score.HomeTeamScore, match.Score.VisitorTeamScore);
                Apply(rows[match.VisitorTeamId], match.Score.VisitorTeamScore, match.Score.HomeTeamScore);
            }

            var ranked = rows.Values
                .OrderByDescending(row => row.Points)
                .ThenByDescending(row => row.GoalDifference)
                .ThenByDescending(row => row.GoalsFor)
                .ThenBy(row => row.TeamName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
                ranked[i].Rank = i + 1;

            result.AddRange(ranked);
        }

        return result;
    }

    private static void Apply(GroupStanding row, int scored, int conceded)
    {
        row.Played++;
        row.GoalsFor += scored;
        row.GoalsAgainst += conceded;
        if (scored > conceded)
        {
            row.Won++;
            row.Points += 3;
        }
        else if (scored == conceded)
        {
            row.Drawn++;
            row.Points += 1;
        }
        else
        {
            row.Lost++;
        }
    }
}
