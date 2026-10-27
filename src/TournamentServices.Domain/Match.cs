using TournamentServices.Domain.Enums;

namespace TournamentServices.Domain;

public class Match
{
    public const string IdPattern = Team.IdPattern;

    private Match()
    {
    }

    public Match(string id, string tournamentId, string? groupId, string homeTeamId, string visitorTeamId,
        string stage = MatchStages.Group)
    {
        Id = id;
        TournamentId = tournamentId;
        GroupId = groupId;
        HomeTeamId = homeTeamId;
        VisitorTeamId = visitorTeamId;
        Stage = string.IsNullOrWhiteSpace(stage) ? MatchStages.Group : stage;
        Score = new Score();
    }

    public string Id { get; private set; } = string.Empty;
    public string TournamentId { get; private set; } = string.Empty;
    public string? GroupId { get; private set; }
    public string HomeTeamId { get; private set; } = string.Empty;
    public string VisitorTeamId { get; private set; } = string.Empty;
    public string Stage { get; private set; } = MatchStages.Group;
    public Score Score { get; private set; } = new();
    public Winner? Winner { get; private set; }
    public bool IsCompleted { get; private set; }

    public string WinnerTeamId => Winner == Enums.Winner.VISITOR ? VisitorTeamId : HomeTeamId;
    public string LoserTeamId => Winner == Enums.Winner.VISITOR ? HomeTeamId : VisitorTeamId;

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
