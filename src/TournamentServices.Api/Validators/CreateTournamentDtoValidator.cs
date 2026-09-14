using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class CreateTournamentDtoValidator : AbstractValidator<CreateTournamentDto>
{
    public CreateTournamentDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().OverridePropertyName("name");
        RuleFor(x => x.Format!).NotNull()
            .SetValidator(new TournamentFormatDtoValidator()).OverridePropertyName("format");
    }
}
