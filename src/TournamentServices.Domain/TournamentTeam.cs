namespace TournamentServices.Domain;

/// <summary>A participant created specifically for one tournament.</summary>
public sealed class TournamentTeam
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TournamentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
