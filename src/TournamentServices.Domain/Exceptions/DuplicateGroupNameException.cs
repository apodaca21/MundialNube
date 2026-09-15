namespace TournamentServices.Domain.Exceptions;

public class DuplicateGroupNameException(string groupName)
    : Exception($"A group named '{groupName}' already exists in this tournament.");