namespace TournamentServices.Api.Dtos;

public record MundialStandingDto(
    string GroupId,
    string GroupName,
    int Rank,
    string TeamId,
    string TeamName,
    int Played,
    int Won,
    int Drawn,
    int Lost,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points);

public record MundialMatchDto(
    string Id,
    string Stage,
    string? GroupId,
    string HomeTeamId,
    string HomeTeamName,
    string VisitorTeamId,
    string VisitorTeamName,
    ScoreDto Score,
    string? WinnerTeamName);

public record MundialSimulationDto(
    string TournamentId,
    string TournamentName,
    string ChampionTeamId,
    string ChampionName,
    string RunnerUpName,
    string ThirdPlaceName,
    IReadOnlyList<MundialStandingDto> Standings,
    IReadOnlyList<MundialMatchDto> Matches);
