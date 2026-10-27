namespace TournamentServices.Domain;

public class GroupStanding
{
    public string GroupId { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string TeamId { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Points { get; set; }
    public int Rank { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
