using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class UpdateTournamentDtoValidator : AbstractValidator<UpdateTournamentDto>
{
    public UpdateTournamentDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().OverridePropertyName("name");
        RuleFor(x => x.Format!).NotNull()
            .SetValidator(new TournamentFormatDtoValidator()).OverridePropertyName("format");
    }
}
