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
