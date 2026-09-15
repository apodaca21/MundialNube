using TournamentServices.Domain.Enums;

namespace TournamentServices.Domain;

public class Match
{
    public const string IdPattern = Team.IdPattern;

    public Match(string id, string tournamentId, string? groupId, string homeTeamId, string visitorTeamId)
    {
        Id = id;
        TournamentId = tournamentId;
        GroupId = groupId;
        HomeTeamId = homeTeamId;
        VisitorTeamId = visitorTeamId;
        Score = new Score();
    }

    public string Id { get; }
    public string TournamentId { get; }
    public string? GroupId { get; }
    public string HomeTeamId { get; }
    public string VisitorTeamId { get; }
    public Score Score { get; private set; }
    public Winner? Winner { get; private set; }
    public bool IsCompleted { get; private set; }

    public void UpdateScore(Score score)
    {
        Score = score;
        Winner = score.HomeTeamScore == score.VisitorTeamScore
            ? null
            : score.HomeTeamScore > score.VisitorTeamScore
                ? Enums.Winner.HOME
                : Enums.Winner.VISITOR;
        IsCompleted = true;
    }
}