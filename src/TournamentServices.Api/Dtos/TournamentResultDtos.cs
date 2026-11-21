using System.Text.Json.Serialization;

namespace TournamentServices.Api.Dtos;

public record TournamentTeamDto(string Id, string TournamentId, string Name);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CompleteTournamentDto(string? TeamId);

public record TournamentChampionDto(
    long Id, string TournamentId, string TournamentName,
    string TeamId, string TeamName, DateTime CompletedAtUtc);
