namespace TournamentServices.Domain.Exceptions;

public sealed class TeamNotInTournamentException(string tournamentId, string teamId)
    : Exception($"Team '{teamId}' is not a participant in tournament '{tournamentId}'.");
