namespace TournamentServices.Domain.Exceptions;

public sealed class SimulationRuleException(string message) : Exception(message);
