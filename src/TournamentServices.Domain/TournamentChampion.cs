namespace TournamentServices.Domain;

/// <summary>A historical snapshot, retained even when a tournament is renamed or deleted.</summary>
public sealed class TournamentChampion
{
    public long Id { get; init; }
    public string TournamentId { get; init; } = string.Empty;
    public string TournamentName { get; init; } = string.Empty;
    public string TeamId { get; init; } = string.Empty;
    public string TeamName { get; init; } = string.Empty;
    public DateTime CompletedAtUtc { get; init; }
}
