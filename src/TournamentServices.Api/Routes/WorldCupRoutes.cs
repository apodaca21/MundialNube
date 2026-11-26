using System.Text.RegularExpressions;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Api.Routes;

public static class WorldCupRoutes
{
    public static IEndpointRouteBuilder MapWorldCupRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tournaments/{tournamentId}").WithTags("World Cup simulation");
        group.MapPost("/groups/generate", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.GenerateGroupsAsync(tournamentId, ct)));
        group.MapPost("/groups/fill", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.FillGroupsAsync(tournamentId, ct)));
        group.MapPost("/matches/generate", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.GenerateMatchesAsync(tournamentId, ct)));
        group.MapPost("/matches/simulate", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.SimulateGroupsAsync(tournamentId, ct)));
        group.MapGet("/standings", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.GetStandingsAsync(tournamentId, ct)));
        group.MapPost("/knockout", (string tournamentId, IWorldCupSimulation simulation, CancellationToken ct) =>
            Run(tournamentId, () => simulation.PlayKnockoutAsync(tournamentId, ct)));
        return app;
    }

    private static async Task<IResult> Run<T>(string id, Func<Task<T>> action)
    {
        if (!Regex.IsMatch(id, Tournament.IdPattern))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
                { ["tournamentId"] = ["The tournament ID format is invalid."] });
        try { return TypedResults.Ok(await action()); }
        catch (TournamentNotFoundException) { return TypedResults.NotFound(); }
        catch (SimulationRuleException error)
        {
            return TypedResults.Problem(error.Message, statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The tournament is not ready for this operation");
        }
    }
}
