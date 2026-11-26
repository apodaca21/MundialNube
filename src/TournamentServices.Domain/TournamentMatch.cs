namespace TournamentServices.Domain;

/// <summary>Persistent match state, including the group phase (round zero) and knockout rounds.</summary>
public class TournamentMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string HomeTeamId { get; set; } = string.Empty;
    public string VisitorTeamId { get; set; } = string.Empty;
    public int HomeTeamScore { get; set; }
    public int VisitorTeamScore { get; set; }
    public bool IsCompleted { get; set; }
    public int Round { get; set; }
}
