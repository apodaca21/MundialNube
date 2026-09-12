namespace TournamentServices.Domain.Exceptions;

public class TeamNotFoundException : Exception
{
    public TeamNotFoundException(string teamId)
        : base($"Team '{teamId}' was not found.")
    {
    }
}
