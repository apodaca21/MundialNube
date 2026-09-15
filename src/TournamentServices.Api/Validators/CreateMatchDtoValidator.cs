using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class CreateMatchDtoValidator : AbstractValidator<CreateMatchDto>
{
    public CreateMatchDtoValidator()
    {
        RuleFor(dto => dto.HomeTeamId).NotEmpty();
        RuleFor(dto => dto.VisitorTeamId).NotEmpty();
    }
}