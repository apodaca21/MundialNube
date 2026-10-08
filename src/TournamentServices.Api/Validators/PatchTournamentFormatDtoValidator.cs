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
        RuleFor(x => x.MaxGroups)
            .InclusiveBetween(TournamentFormat.MinGroups, TournamentFormat.MaxGroupsLimit)
            .When(x => x.MaxGroups.HasValue).OverridePropertyName("maxGroups");
        RuleFor(x => x.MaxTeamsPerGroup)
            .InclusiveBetween(TournamentFormat.MinTeamsPerGroup, TournamentFormat.MaxTeamsPerGroupLimit)
            .When(x => x.MaxTeamsPerGroup.HasValue).OverridePropertyName("maxTeamsPerGroup");
    }
}
