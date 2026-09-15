namespace TournamentServices.Domain.Exceptions;

public class TournamentNotFoundException : Exception
{
    public TournamentNotFoundException(string tournamentId)
        : base($"Tournament '{tournamentId}' was not found.")
    {
    }
}
