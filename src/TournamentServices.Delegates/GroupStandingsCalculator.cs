using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public static class GroupStandingsCalculator
{
    public static IReadOnlyList<GroupStanding> Calculate(
        IReadOnlyList<Group> groups,
        IReadOnlyList<Match> matches,
        IReadOnlyDictionary<string, string> teamNames)
    {
        var result = new List<GroupStanding>();
        foreach (var group in groups)
        {
            var rows = group.TeamIds.Select(teamId => new GroupStanding
            {
                GroupId = group.Id,
                GroupName = group.Name,
                TeamId = teamId,
                TeamName = teamNames.TryGetValue(teamId, out var name) ? name : teamId
            }).ToDictionary(row => row.TeamId, StringComparer.Ordinal);

            foreach (var match in matches.Where(item =>
                         item.Stage == MatchStages.Group &&
                         item.GroupId == group.Id &&
                         item.IsCompleted &&
                         rows.ContainsKey(item.HomeTeamId) &&
                         rows.ContainsKey(item.VisitorTeamId)))
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
            row.Points++;
        }
        else
        {
            row.Lost++;
        }
    }
}
