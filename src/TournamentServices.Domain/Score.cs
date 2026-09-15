namespace TournamentServices.Domain;

public sealed record Score
{
    public Score(int homeTeamScore = 0, int visitorTeamScore = 0)
    {
        if (homeTeamScore < 0 || visitorTeamScore < 0)
            throw new ArgumentOutOfRangeException(nameof(homeTeamScore), "Scores cannot be negative.");

        HomeTeamScore = homeTeamScore;
        VisitorTeamScore = visitorTeamScore;
    }

    public int HomeTeamScore { get; }
    public int VisitorTeamScore { get; }
}