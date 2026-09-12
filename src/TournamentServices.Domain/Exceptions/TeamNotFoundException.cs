namespace TournamentServices.Domain.Exceptions;

public class TeamNotFoundException : Exception
{
    public string TeamId { get; }

    public TeamNotFoundException(string teamId)
        : base($"Team '{teamId}' was not found.")
    {
        TeamId = teamId;
    }
}
