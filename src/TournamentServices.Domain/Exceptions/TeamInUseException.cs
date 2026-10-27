namespace TournamentServices.Domain.Exceptions;

public class TeamInUseException : Exception
{
    public TeamInUseException(string teamId)
        : base($"Team '{teamId}' is assigned to a group or match and cannot be deleted.")
    {
    }
}
