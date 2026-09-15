using System.Text.Json.Serialization;

namespace TournamentServices.Api.Dtos;

public record GroupDto(string Id, string Name, string TournamentId, IReadOnlyList<TeamDto> Teams);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateGroupDto(string? Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateGroupDto(string? Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record AssignTeamsDto(IReadOnlyList<string>? TeamIds);