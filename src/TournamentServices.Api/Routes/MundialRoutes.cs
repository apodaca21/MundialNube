using System.Text.RegularExpressions;
using TournamentServices.Api.Dtos;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Api.Routes;

public static class MundialRoutes
{
    public static IEndpointRouteBuilder MapMundialRoutes(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tournaments/{tournamentId}/mundial", Simulate)
            .WithTags("Mundial");
        return app;
    }

    private static async Task<IResult> Simulate(string tournamentId, IMundialDelegate mundial,
        ITeamDelegate teams, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(tournamentId, Tournament.IdPattern))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["tournamentId"] = ["The tournament ID format is invalid."] });
        }

        try
        {
            var result = await mundial.SimulateAsync(tournamentId, cancellationToken);
            var names = (await teams.GetAllAsync(cancellationToken))
                .ToDictionary(team => team.Id, team => team.Name, StringComparer.Ordinal);
            return TypedResults.Ok(ToDto(result, names));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static MundialSimulationDto ToDto(MundialSimulation result, IReadOnlyDictionary<string, string> names)
    {
        var standings = result.Standings.Select(row => new MundialStandingDto(
            row.GroupId, row.GroupName, row.Rank, row.TeamId, row.TeamName,
            row.Played, row.Won, row.Drawn, row.Lost, row.GoalsFor, row.GoalsAgainst,
            row.GoalDifference, row.Points)).ToList();

        var matches = result.Matches.Select(match => new MundialMatchDto(
            match.Id,
            match.Stage,
            match.GroupId,
            match.HomeTeamId,
            NameOf(names, match.HomeTeamId),
            match.VisitorTeamId,
            NameOf(names, match.VisitorTeamId),
            new ScoreDto(match.Score.HomeTeamScore, match.Score.VisitorTeamScore),
            match.Winner is null ? null : NameOf(names, match.WinnerTeamId))).ToList();

        return new MundialSimulationDto(
            result.Tournament.Id,
            result.Tournament.Name,
            result.ChampionTeamId,
            result.ChampionName,
            result.RunnerUpName,
            result.ThirdPlaceName,
            standings,
            matches);
    }

    private static string NameOf(IReadOnlyDictionary<string, string> names, string teamId)
        => names.TryGetValue(teamId, out var name) ? name : teamId;
}
