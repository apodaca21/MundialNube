namespace TournamentServices.Domain.Exceptions;

public class MatchNotFoundException(string matchId)
    : Exception($"Match '{matchId}' was not found.");