namespace TournamentServices.Domain.Exceptions;

public class DuplicateTeamNameException : Exception
{
    public string TeamName { get; }

    public DuplicateTeamNameException(string teamName)
        : base($"A team named '{teamName}' already exists.")
    {
        TeamName = teamName;
    }
}
