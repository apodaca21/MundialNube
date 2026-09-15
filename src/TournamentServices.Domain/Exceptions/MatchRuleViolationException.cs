namespace TournamentServices.Domain.Exceptions;

public class MatchRuleViolationException(string message) : Exception(message);