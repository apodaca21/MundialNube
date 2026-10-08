using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;

namespace TournamentServices.Api.Validators;

public class PatchTournamentFormatDtoValidator : AbstractValidator<PatchTournamentFormatDto>
{
    public PatchTournamentFormatDtoValidator()
    {
        RuleFor(x => x.Type).Equal(TournamentFormat.RoundRobin)
            .When(x => x.Type is not null).OverridePropertyName("type");
        RuleFor(x => x.RequiredGroups)
            .Equal(TournamentFormat.RequiredGroups)
            .When(x => x.RequiredGroups.HasValue).OverridePropertyName("RequiredGroups");
        RuleFor(x => x.RequiredTeamsPerGroup)
            .Equal(TournamentFormat.RequiredTeamsPerGroup)
            .When(x => x.RequiredTeamsPerGroup.HasValue).OverridePropertyName("RequiredTeamsPerGroup");
    }
}
