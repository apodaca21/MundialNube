using System.Text.RegularExpressions;
using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Delegates;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using DomainMatch = TournamentServices.Domain.Match;

namespace TournamentServices.Api.Routes;

public static class MatchRoutes
{
    public static IEndpointRouteBuilder MapMatchRoutes(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tournaments/{tournamentId}/matches").WithTags("Matches");
        group.MapGet("/", GetAll);
        group.MapGet("/{matchId}", GetById);
        group.MapPost("/", Create);
        group.MapPatch("/{matchId}/score", UpdateScore);
        group.MapDelete("/{matchId}", Delete);
        return app;
    }

    private static async Task<IResult> GetAll(string tournamentId, IMatchDelegate matches, ITeamDelegate teams,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");

        try
        {
            var result = await matches.GetByTournamentAsync(tournamentId, cancellationToken);
            return TypedResults.Ok(await Task.WhenAll(result.Select(match => ToDto(match, teams, cancellationToken))));
        }
        catch (TournamentNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> GetById(string tournamentId, string matchId, IMatchDelegate matches,
        ITeamDelegate teams, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(matchId))
            return InvalidId("matchId");

        var result = await matches.GetByIdAsync(tournamentId, matchId, cancellationToken);
        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(await ToDto(result, teams, cancellationToken));
    }

    private static async Task<IResult> Create(string tournamentId, CreateMatchDto dto, IMatchDelegate matches,
        IValidator<CreateMatchDto> validator, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        if (dto.GroupId is not null && !IsValidId(dto.GroupId))
            return InvalidId("groupId");
        if (!IsValidId(dto.HomeTeamId!) || !IsValidId(dto.VisitorTeamId!))
            return InvalidId("teamId");

        try
        {
            var id = await matches.CreateAsync(tournamentId, dto.GroupId, dto.HomeTeamId!, dto.VisitorTeamId!, cancellationToken);
            var created = await matches.GetByIdAsync(tournamentId, id, cancellationToken);
            return TypedResults.Created($"/tournaments/{tournamentId}/matches/{id}", ToDto(created!));
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
        catch (MatchRuleViolationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> UpdateScore(string tournamentId, string matchId, UpdateScoreDto dto,
        IMatchDelegate matches, IValidator<UpdateScoreDto> validator, CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(matchId))
            return InvalidId("matchId");

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ToValidationProblem(validation);

        try
        {
            var updated = await matches.UpdateScoreAsync(tournamentId, matchId,
                new Score(dto.HomeTeamScore, dto.VisitorTeamScore), cancellationToken);
            return TypedResults.Ok(ToDto(updated));
        }
        catch (MatchNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static async Task<IResult> Delete(string tournamentId, string matchId, IMatchDelegate matches,
        CancellationToken cancellationToken)
    {
        if (!IsValidId(tournamentId))
            return InvalidId("tournamentId");
        if (!IsValidId(matchId))
            return InvalidId("matchId");

        try
        {
            await matches.DeleteAsync(tournamentId, matchId, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (MatchNotFoundException)
        {
            return TypedResults.NotFound();
        }
    }

    private static MatchDto ToDto(DomainMatch match)
        => new(match.Id, match.TournamentId, match.GroupId, match.HomeTeamId, match.VisitorTeamId,
            null, null, new ScoreDto(match.Score.HomeTeamScore, match.Score.VisitorTeamScore),
            match.Winner, match.IsCompleted);

    private static async Task<MatchDto> ToDto(DomainMatch match, ITeamDelegate teams, CancellationToken cancellationToken)
    {
        var home = await teams.GetByIdAsync(match.HomeTeamId, cancellationToken);
        var visitor = await teams.GetByIdAsync(match.VisitorTeamId, cancellationToken);
        return new MatchDto(match.Id, match.TournamentId, match.GroupId, match.HomeTeamId, match.VisitorTeamId,
            home is null ? null : new TeamDto(home.Id, home.Name),
            visitor is null ? null : new TeamDto(visitor.Id, visitor.Name),
            new ScoreDto(match.Score.HomeTeamScore, match.Score.VisitorTeamScore), match.Winner, match.IsCompleted);
    }

    private static bool IsValidId(string id) => Regex.IsMatch(id, Team.IdPattern);

    private static IResult InvalidId(string field) => TypedResults.ValidationProblem(
        new Dictionary<string, string[]> { [field] = [$"The {field} format is invalid."] });

    private static IResult ToValidationProblem(FluentValidation.Results.ValidationResult result)
        => TypedResults.ValidationProblem(result.Errors.GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
}