namespace TournamentServices.Domain.Exceptions;

public class GroupNotFoundException(string groupId)
    : Exception($"Group '{groupId}' was not found.");