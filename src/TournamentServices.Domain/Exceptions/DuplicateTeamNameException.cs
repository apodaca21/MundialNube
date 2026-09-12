namespace TournamentServices.Domain.Exceptions;

public class DuplicateTeamNameException : Exception
{
    public DuplicateTeamNameException(string teamName)
        : base($"A team named '{teamName}' already exists.")
    {
    }
}
