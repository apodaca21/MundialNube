namespace TournamentServices.Domain.Exceptions;

public class DuplicateTournamentNameException : Exception
{
    public DuplicateTournamentNameException(string tournamentName)
        : base($"A tournament named '{tournamentName}' already exists.")
    {
    }
}
