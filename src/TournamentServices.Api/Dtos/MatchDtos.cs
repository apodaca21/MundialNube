using System.Text.Json.Serialization;
using TournamentServices.Domain.Enums;

namespace TournamentServices.Api.Dtos;

public record ScoreDto(int HomeTeamScore, int VisitorTeamScore);

public record MatchDto(
    string Id,
    string TournamentId,
    string? GroupId,
    string HomeTeamId,
    string VisitorTeamId,
    TeamDto? HomeTeam,
    TeamDto? VisitorTeam,
    ScoreDto Score,
    Winner? Winner,
    bool IsCompleted);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateMatchDto(string? GroupId, string? HomeTeamId, string? VisitorTeamId);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateScoreDto(int HomeTeamScore, int VisitorTeamScore);