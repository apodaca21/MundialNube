using System.Text.RegularExpressions;
using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Api.Routes;

public static class TournamentRoutes
{
    public static IEndpointRouteBuilder MapTournamentRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tournaments").WithTags("Tournaments");
        group.MapGet("/", GetAll);
        group.MapGet("/{tournamentId}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{tournamentId}", Update);
        group.MapPatch("/{tournamentId}", Patch);
        group.MapDelete("/{tournamentId}", Delete);
        return app;
    }

    private static async Task<IResult> GetAll(ITournamentDelegate tournaments, ITeamDelegate teams, CancellationToken ct)
    {
        var result = await tournaments.GetAllAsync(ct);
        var lookup = await TeamLookup(teams, ct);
        return TypedResults.Ok(result.Select(item => ToDto(item, lookup)).ToList());
    }

    private static async Task<IResult> GetById(string tournamentId, ITournamentDelegate tournaments,
        ITeamDelegate teams, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return InvalidId();

        var tournament = await tournaments.GetByIdAsync(tournamentId, ct);
        return tournament is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(tournament, await TeamLookup(teams, ct)));
    }

    private static async Task<IResult> Create(
        CreateTournamentDto dto, ITournamentDelegate tournaments,
        IValidator<CreateTournamentDto> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var created = await tournaments.CreateAsync(dto.Name!, ToFormat(dto.Format!), ct);
            return TypedResults.Created($"/tournaments/{created.Id}", ToDto(created, new Dictionary<string, Team>()));
        }
        catch (DuplicateTournamentNameException ex)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["name"] = [ex.Message] });
        }
    }

    private static async Task<IResult> Update(
        string tournamentId, UpdateTournamentDto dto, ITournamentDelegate tournaments,
        IValidator<UpdateTournamentDto> validator, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return InvalidId();

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var updated = await tournaments.UpdateAsync(tournamentId, dto.Name!, ToFormat(dto.Format!), ct);
            return TypedResults.Ok(ToDto(updated, new Dictionary<string, Team>()));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateTournamentNameException ex)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["name"] = [ex.Message] });
        }
    }

    private static async Task<IResult> Patch(
        string tournamentId, PatchTournamentDto dto, ITournamentDelegate tournaments,
        IValidator<PatchTournamentDto> validator, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return InvalidId();

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var updated = await tournaments.PatchAsync(tournamentId, dto.Name,
                dto.Format?.Type, dto.Format?.MaxGroups, dto.Format?.MaxTeamsPerGroup, ct);
            return TypedResults.Ok(ToDto(updated, new Dictionary<string, Team>()));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateTournamentNameException ex)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]> { ["name"] = [ex.Message] });
        }
    }

    private static async Task<IResult> Delete(string tournamentId, ITournamentDelegate tournaments, CancellationToken ct)
    {
        if (!IsValidId(tournamentId))
            return InvalidId();

        try
        {
            await tournaments.DeleteAsync(tournamentId, ct);
            return TypedResults.NoContent();
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<Dictionary<string, Team>> TeamLookup(ITeamDelegate teams, CancellationToken ct)
    {
        var all = await teams.GetAllAsync(ct);
        return all.ToDictionary(team => team.Id);
    }

    private static TournamentDto ToDto(Tournament tournament, IReadOnlyDictionary<string, Team> teams)
    {
        var groups = tournament.Groups.Select(group => new GroupDto(
            group.Id, group.Name, group.TournamentId,
            group.TeamIds.Where(teams.ContainsKey)
                .Select(id => new TeamDto(teams[id].Id, teams[id].Name)).ToList())).ToList();

        var matches = tournament.Matches.Select(match =>
        {
            teams.TryGetValue(match.HomeTeamId, out var home);
            teams.TryGetValue(match.VisitorTeamId, out var visitor);
            return new MatchDto(match.Id, match.TournamentId, match.GroupId, match.HomeTeamId, match.VisitorTeamId,
                home is null ? null : new TeamDto(home.Id, home.Name),
                visitor is null ? null : new TeamDto(visitor.Id, visitor.Name),
                new ScoreDto(match.Score.HomeTeamScore, match.Score.VisitorTeamScore),
                match.Winner, match.IsCompleted, match.Stage);
        }).ToList();

        return new TournamentDto(
            tournament.Id,
            tournament.Name,
            new TournamentFormatDto(tournament.Format.Type, tournament.Format.MaxGroups, tournament.Format.MaxTeamsPerGroup),
            groups,
            matches);
    }

    private static TournamentFormat ToFormat(TournamentFormatDto format)
        => new(format.Type, format.MaxGroups, format.MaxTeamsPerGroup);

    private static bool IsValidId(string id) => Regex.IsMatch(id, Tournament.IdPattern);

    private static IResult InvalidId() => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { ["tournamentId"] = ["The tournament ID format is invalid."] });

    private static IResult ToValidationProblem(FluentValidation.Results.ValidationResult result)
    {
        var errors = result.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        return TypedResults.ValidationProblem(errors);
    }
}
