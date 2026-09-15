namespace TournamentServices.Domain.Exceptions;

public class GroupRuleViolationException(string message) : Exception(message);