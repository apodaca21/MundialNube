namespace TournamentServices.Repositories;

public sealed class GroupTeam
{
    public string GroupId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
}

public sealed class OutboxEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}

public sealed class ConsumedEvent
{
    public string Id { get; set; } = string.Empty;
    public DateTime ConsumedAtUtc { get; set; }
    public string Type { get; set; } = string.Empty;
}
