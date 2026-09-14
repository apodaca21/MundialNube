namespace TournamentServices.Domain;

public class TournamentGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
