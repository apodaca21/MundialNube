using System.Text.RegularExpressions;
using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using DomainGroup = TournamentServices.Domain.Group;

namespace TournamentServices.Api.Routes;

public static class GroupRoutes
{
    public static IEndpointRouteBuilder MapGroupRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tournaments/{tournamentId}/groups").WithTags("Groups");
        group.MapGet("/", GetAll);
        group.MapGet("/{groupId}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{groupId}", Update);
        group.MapDelete("/{groupId}", Delete);
        group.MapPatch("/{groupId}/teams", AssignTeams);
        return app;
    }

    private static async Task<IResult> GetAll(string tournamentId, IGroupDelegate groups, ITeamDelegate teams,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");

        try
        {
            var result = await groups.GetByTournamentAsync(tournamentId, cancellationToken);
            return TypedResults.Ok(await Task.WhenAll(result.Select(group => ToDto(group, teams, cancellationToken))));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetById(string tournamentId, string groupId, IGroupDelegate groups,
        ITeamDelegate teams, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(groupId))
            return InvalidId("groupId");

        var result = await groups.GetByIdAsync(tournamentId, groupId, cancellationToken);
        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(await ToDto(result, teams, cancellationToken));
    }

    private static async Task<IResult> Create(string tournamentId, CreateGroupDto dto, IGroupDelegate groups,
        IValidator<CreateGroupDto> validator, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var id = await groups.CreateAsync(tournamentId, dto.Name!, cancellationToken);
            var created = await groups.GetByIdAsync(tournamentId, id, cancellationToken);
            return TypedResults.Created($"/tournaments/{tournamentId}/groups/{id}",
                await ToDto(created!, teams: null, cancellationToken));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateGroupNameException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (GroupRuleViolationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> Update(string tournamentId, string groupId, UpdateGroupDto dto,
        IGroupDelegate groups, ITeamDelegate teams, IValidator<UpdateGroupDto> validator,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(groupId))
            return InvalidId("groupId");

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var updated = await groups.UpdateAsync(tournamentId, groupId, dto.Name!, cancellationToken);
            return TypedResults.Ok(await ToDto(updated, teams, cancellationToken));
        }
        catch (GroupNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (DuplicateGroupNameException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> AssignTeams(string tournamentId, string groupId, AssignTeamsDto dto,
        IGroupDelegate groups, IValidator<AssignTeamsDto> validator, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(groupId))
            return InvalidId("groupId");

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        var invalidTeamId = dto.TeamIds!.FirstOrDefault(teamId => !IsValidId(teamId));
        if (invalidTeamId is not null)
            return InvalidId("teamIds");

        try
        {
            await groups.AssignTeamsAsync(tournamentId, groupId, dto.TeamIds!, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (GroupNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (TeamNotFoundException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (GroupRuleViolationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> Delete(string tournamentId, string groupId, IGroupDelegate groups,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(groupId))
            return InvalidId("groupId");

        try
        {
            await groups.DeleteAsync(tournamentId, groupId, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (GroupNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<GroupDto> ToDto(DomainGroup group, ITeamDelegate? teams, CancellationToken cancellationToken)
    {
        if (teams is null)
            return new GroupDto(group.Id, group.Name, group.TournamentId, []);

        var teamResults = await Task.WhenAll(group.TeamIds.Select(teamId => teams.GetByIdAsync(teamId, cancellationToken)));
        return new GroupDto(group.Id, group.Name, group.TournamentId,
            teamResults.Where(team => team is not null).Select(team => new TeamDto(team!.Id, team.Name)).ToList());
    }

    private static bool IsValidId(string id) => Regex.IsMatch(id, Team.IdPattern);

    private static IResult InvalidId(string field) => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [$"The {field} format is invalid."] });

    private static IResult ToValidationProblem(FluentValidation.Results.ValidationResult result)
        => TypedResults.ValidationProblem(result.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
}