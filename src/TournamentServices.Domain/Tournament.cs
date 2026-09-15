namespace TournamentServices.Domain;

public class Tournament
{
    public const string IdPattern = @"^[A-Za-z0-9\-]+$";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public TournamentFormat Format { get; set; } = new(TournamentFormat.RoundRobin, 8, 4);
}
