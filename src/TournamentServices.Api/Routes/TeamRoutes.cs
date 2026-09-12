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

        group.MapGet("/", GetAllAsync);
        group.MapGet("/{teamId}", GetByIdAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{teamId}", UpdateAsync);
        group.MapDelete("/{teamId}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        ITeamDelegate teamDelegate,
        CancellationToken cancellationToken)
    {
        var teams = await teamDelegate.GetAllAsync(cancellationToken);
        return TypedResults.Ok(teams.Select(ToDto));
    }

    private static async Task<IResult> GetByIdAsync(
        string teamId,
        ITeamDelegate teamDelegate,
        CancellationToken cancellationToken)
    {
        if (!IsValidTeamId(teamId))
        {
            return InvalidTeamId();
        }

        var team = await teamDelegate.GetByIdAsync(teamId, cancellationToken);
        return team is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(team));
    }

    private static async Task<IResult> CreateAsync(
        CreateTeamDto dto,
        ITeamDelegate teamDelegate,
        IValidator<CreateTeamDto> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        try
        {
            var id = await teamDelegate.CreateAsync(dto.Name, cancellationToken);
            var created = await teamDelegate.GetByIdAsync(id, cancellationToken);
            return TypedResults.Created($"/teams/{id}", ToDto(created!));
        }
        catch (DuplicateTeamNameException exception)
        {
            return DuplicateName(exception);
        }
    }

    private static async Task<IResult> UpdateAsync(
        string teamId,
        UpdateTeamDto dto,
        ITeamDelegate teamDelegate,
        IValidator<UpdateTeamDto> validator,
        CancellationToken cancellationToken)
    {
        if (!IsValidTeamId(teamId))
        {
            return InvalidTeamId();
        }

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationFailed(validation);
        }

        try
        {
            var updated = await teamDelegate.UpdateAsync(teamId, dto.Name, cancellationToken);
            return TypedResults.Ok(ToDto(updated));
        }
        catch (TeamNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateTeamNameException exception)
        {
            return DuplicateName(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string teamId,
        ITeamDelegate teamDelegate,
        CancellationToken cancellationToken)
    {
        if (!IsValidTeamId(teamId))
        {
            return InvalidTeamId();
        }

        try
        {
            await teamDelegate.DeleteAsync(teamId, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (TeamNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static TeamDto ToDto(Team team) => new(team.Id, team.Name);

    private static bool IsValidTeamId(string teamId)
        => Regex.IsMatch(teamId, Team.IdPattern, RegexOptions.CultureInvariant);

    private static IResult InvalidTeamId()
        => ValidationFailed("teamId", "The team ID format is invalid.");

    private static IResult DuplicateName(DuplicateTeamNameException exception)
        => ValidationFailed("name", exception.Message);

    private static IResult ValidationFailed(FluentValidation.Results.ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return ValidationProblem(errors);
    }

    private static IResult ValidationFailed(string field, string message)
        => ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

    private static IResult ValidationProblem(IDictionary<string, string[]> errors)
        => TypedResults.ValidationProblem(
            errors,
            title: "Validation Failed",
            type: "https://tools.ietf.org/html/rfc7231#section-6.5.1");
}
