using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class PatchTournamentDtoValidator : AbstractValidator<PatchTournamentDto>
{
    public PatchTournamentDtoValidator()
    {
        RuleFor(x => x).Must(x => x.Name is not null || x.Format?.Type is not null
                || x.Format?.MaxGroups is not null || x.Format?.MaxTeamsPerGroup is not null)
            .WithMessage("Provide at least one field to update.").OverridePropertyName("body");
        RuleFor(x => x.Name).NotEmpty().When(x => x.Name is not null).OverridePropertyName("name");
        RuleFor(x => x.Format!).SetValidator(new PatchTournamentFormatDtoValidator())
            .OverridePropertyName("format");
    }
}
