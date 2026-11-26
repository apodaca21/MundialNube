namespace TournamentServices.Domain;

public sealed record TeamStanding(string TeamId, string TeamName, int Played, int Wins, int Draws,
    int Losses, int GoalsFor, int GoalsAgainst)
{
    public int Points => Wins * 3 + Draws;
    public int GoalDifference => GoalsFor - GoalsAgainst;
}

public sealed record GroupStanding(string GroupId, string GroupName, IReadOnlyList<TeamStanding> Teams);
public sealed record KnockoutPair(string HomeTeamId, string VisitorTeamId);
public sealed record KnockoutPairing(IReadOnlyList<string> Byes, IReadOnlyList<KnockoutPair> Matches);

public static class WorldCupRules
{
    public static IReadOnlyList<TeamStanding> Order(IEnumerable<TeamStanding> teams) => teams
        .OrderByDescending(team => team.Wins).ThenByDescending(team => team.GoalsFor)
        .ThenBy(team => team.GoalsAgainst).ThenBy(team => team.TeamName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(team => team.TeamId, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<TeamStanding> Standings(IEnumerable<Team> teams, IEnumerable<Match> matches)
    {
        var completed = matches.Where(match => match.Round == 0 && match.IsCompleted).ToList();
        return Order(teams.Select(team =>
        {
            var played = completed.Where(match => match.HomeTeamId == team.Id || match.VisitorTeamId == team.Id).ToList();
            var scores = played.Select(match => match.HomeTeamId == team.Id
                ? (For: match.Score.HomeTeamScore, Against: match.Score.VisitorTeamScore)
                : (For: match.Score.VisitorTeamScore, Against: match.Score.HomeTeamScore)).ToList();
            return new TeamStanding(team.Id, team.Name, scores.Count, scores.Count(score => score.For > score.Against),
                scores.Count(score => score.For == score.Against), scores.Count(score => score.For < score.Against),
                scores.Sum(score => score.For), scores.Sum(score => score.Against));
        }));
    }

    // Input is already seeded from best to worst. Highest seeds receive the byes.
    public static KnockoutPairing Pair(IReadOnlyList<string> rankedTeams)
    {
        if (rankedTeams.Count < 2 || rankedTeams.Distinct().Count() != rankedTeams.Count)
            throw new ArgumentException("At least two distinct teams are required.", nameof(rankedTeams));
        var nextPower = 1;
        while (nextPower < rankedTeams.Count) nextPower *= 2;
        var byeCount = nextPower - rankedTeams.Count;
        var byes = rankedTeams.Take(byeCount).ToList();
        var active = rankedTeams.Skip(byeCount).ToList();
        var pairs = Enumerable.Range(0, active.Count / 2)
            .Select(index => new KnockoutPair(active[index], active[active.Count - 1 - index])).ToList();
        return new KnockoutPairing(byes, pairs);
    }
}
