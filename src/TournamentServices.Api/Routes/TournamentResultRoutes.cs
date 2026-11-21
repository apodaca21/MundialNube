using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Routes;

public static class TournamentResultRoutes
{
    private const int ReportLimit = 20;

    public static IEndpointRouteBuilder MapTournamentResultRoutes(this IEndpointRouteBuilder app)
    {
        var tournaments = app.MapGroup("/tournaments").WithTags("Tournament results");
        tournaments.MapGet("/{tournamentId}/teams", GetTeams);
        tournaments.MapPost("/{tournamentId}/champion", Complete);

        var reports = app.MapGroup("/reports").WithTags("Reports");
        reports.MapGet("/champions", GetChampions);
        reports.MapGet("/champions/latest", GetLatestChampion);
        reports.MapGet("/champions.csv", DownloadChampions);
        return app;
    }

    private static async Task<IResult> GetTeams(
        string tournamentId, ITournamentResultsRepository results, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return FieldError("tournamentId", "The tournament ID format is invalid.");

        var teams = await results.GetTeamsAsync(tournamentId, ct);
        return teams is null ? TypedResults.NotFound() : TypedResults.Ok(
            teams.Select(team => new TournamentTeamDto(team.Id, team.TournamentId, team.Name)));
    }

    private static async Task<IResult> Complete(
        string tournamentId, CompleteTournamentDto dto,
        ITournamentResultsRepository results, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return FieldError("tournamentId", "The tournament ID format is invalid.");
        if (!IsValidId(dto.TeamId))
            return FieldError("teamId", "A valid team ID is required.");

        try
        {
            return TypedResults.Ok(ToDto(await results.CompleteAsync(tournamentId, dto.TeamId!, ct)));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (TeamNotInTournamentException ex)
        {
            return FieldError("teamId", ex.Message);
        }
        catch (TournamentAlreadyCompletedException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Tournament already completed", detail: ex.Message);
        }
    }

    private static async Task<IResult> GetChampions(
        ITournamentResultsRepository results, CancellationToken ct)
    {
        var champions = await results.GetRecentChampionsAsync(ReportLimit, ct);
        return TypedResults.Ok(champions.Select(ToDto));
    }

    private static async Task<IResult> GetLatestChampion(
        ITournamentResultsRepository results, CancellationToken ct)
    {
        var champions = await results.GetRecentChampionsAsync(1, ct);
        return champions.Count == 0 ? TypedResults.NotFound() : TypedResults.Ok(ToDto(champions[0]));
    }

    private static async Task<IResult> DownloadChampions(
        ITournamentResultsRepository results, CancellationToken ct)
    {
        var champions = await results.GetRecentChampionsAsync(ReportLimit, ct);
        var csv = new StringBuilder("tournamentId,tournamentName,teamId,teamName,completedAtUtc\r\n");
        foreach (var champion in champions)
        {
            csv.AppendJoin(',', Csv(champion.TournamentId), Csv(champion.TournamentName),
                Csv(champion.TeamId), Csv(champion.TeamName),
                Csv(champion.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            csv.Append("\r\n");
        }

        return TypedResults.File(Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv; charset=utf-8", "ultimos-20-campeones.csv");
    }

    private static TournamentChampionDto ToDto(TournamentChampion champion) => new(
        champion.Id, champion.TournamentId, champion.TournamentName,
        champion.TeamId, champion.TeamName, champion.CompletedAtUtc);

    private static bool IsValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && Regex.IsMatch(id, Tournament.IdPattern);

    private static IResult FieldError(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static string Csv(string value)
    {
        // Keep spreadsheet applications from interpreting stored names as formulas.
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0]) ||
            value.Length > 0 && "\t\r\n".Contains(value[0]))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
