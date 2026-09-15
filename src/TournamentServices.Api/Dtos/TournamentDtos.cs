using System.Text.Json.Serialization;

namespace TournamentServices.Api.Dtos;

public record TournamentDto(string Id, string Name, TournamentFormatDto Format);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record TournamentFormatDto(string Type, int MaxGroups, int MaxTeamsPerGroup);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateTournamentDto(string? Name, TournamentFormatDto? Format);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateTournamentDto(string? Name, TournamentFormatDto? Format);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record PatchTournamentDto(string? Name = null, PatchTournamentFormatDto? Format = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record PatchTournamentFormatDto(string? Type = null, int? MaxGroups = null, int? MaxTeamsPerGroup = null);
