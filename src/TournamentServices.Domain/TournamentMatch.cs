namespace TournamentServices.Domain;

public class TournamentMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string GroupId { get; set; } = string.Empty;
}
