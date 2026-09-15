using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class AssignTeamsDtoValidator : AbstractValidator<AssignTeamsDto>
{
    public AssignTeamsDtoValidator()
    {
        RuleFor(dto => dto.TeamIds).NotEmpty();
        RuleForEach(dto => dto.TeamIds).NotEmpty();
    }
}