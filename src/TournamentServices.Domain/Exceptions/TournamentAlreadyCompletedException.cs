namespace TournamentServices.Domain.Exceptions;

public sealed class TournamentAlreadyCompletedException(string tournamentId)
    : Exception($"Tournament '{tournamentId}' already has a different champion.");
