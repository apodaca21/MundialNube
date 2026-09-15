using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class UpdateGroupDtoValidator : AbstractValidator<UpdateGroupDto>
{
    public UpdateGroupDtoValidator()
    {
        RuleFor(dto => dto.Name).NotEmpty().MaximumLength(100);
    }
}