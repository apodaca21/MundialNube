using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class UpdateTeamDtoValidator : AbstractValidator<UpdateTeamDto>
{
    public UpdateTeamDtoValidator()
    {
        RuleFor(dto => dto.Name)
            .NotEmpty()
            .WithMessage("Name is required.");
    }
}
