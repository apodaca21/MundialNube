using System.Text.RegularExpressions;
using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;

namespace TournamentServices.Api.Routes;

public static class TeamRoutes
{
    public static IEndpointRouteBuilder MapTeamRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/teams");

        group.MapGet("/", GetAll);
        group.MapGet("/{teamId}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{teamId}", Update);
        group.MapDelete("/{teamId}", Delete);

        return app;
    }

    private static async Task<IResult> GetAll(ITeamDelegate teams, CancellationToken ct)
    {
        var result = await teams.GetAllAsync(ct);
        return TypedResults.Ok(result.Select(ToDto));
    }

    private static async Task<IResult> GetById(string teamId, ITeamDelegate teams, CancellationToken ct)
    {
        if (!IsValidId(teamId))
            return InvalidId();

        var team = await teams.GetByIdAsync(teamId, ct);
        return team == null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(team));
    }

    private static async Task<IResult> Create(
        CreateTeamDto dto,
        ITeamDelegate teams,
        IValidator<CreateTeamDto> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var id = await teams.CreateAsync(dto.Name, ct);
            var created = await teams.GetByIdAsync(id, ct);
            return TypedResults.Created($"/teams/{id}", ToDto(created!));
        }
        catch (DuplicateTeamNameException ex)
        {
            return FieldError("name", ex.Message);
        }
    }

    private static async Task<IResult> Update(
        string teamId,
        UpdateTeamDto dto,
        ITeamDelegate teams,
        IValidator<UpdateTeamDto> validator,
        CancellationToken ct)
    {
        if (!IsValidId(teamId))
            return InvalidId();

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var updated = await teams.UpdateAsync(teamId, dto.Name, ct);
            return TypedResults.Ok(ToDto(updated));
        }
        catch (TeamNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateTeamNameException ex)
        {
            return FieldError("name", ex.Message);
        }
    }

    private static async Task<IResult> Delete(string teamId, ITeamDelegate teams, CancellationToken ct)
    {
        if (!IsValidId(teamId))
            return InvalidId();

        try
        {
            await teams.DeleteAsync(teamId, ct);
            return TypedResults.NoContent();
        }
        catch (TeamNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static TeamDto ToDto(Team team) => new(team.Id, team.Name);

    private static bool IsValidId(string id) => Regex.IsMatch(id, Team.IdPattern);

    private static IResult InvalidId() => FieldError("teamId", "The team ID format is invalid.");

    private static IResult ToValidationProblem(FluentValidation.Results.ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return TypedResults.ValidationProblem(errors);
    }

    private static IResult FieldError(string field, string message)
        => TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
