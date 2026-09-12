using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class CreateTeamDtoValidator : AbstractValidator<CreateTeamDto>
{
    public CreateTeamDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .WithMessage("Name is required.");
    }
}
