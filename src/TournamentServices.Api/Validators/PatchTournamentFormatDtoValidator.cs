using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;

namespace TournamentServices.Api.Validators;

public class PatchTournamentFormatDtoValidator : AbstractValidator<PatchTournamentFormatDto>
{
    public PatchTournamentFormatDtoValidator()
    {
        RuleFor(x => x.Type)
            .Must(type => type == TournamentFormat.Fifa)
            .When(x => x.Type is not null)
            .WithMessage("The supported tournament format is FIFA.")
            .OverridePropertyName("type");
        RuleFor(x => x.MaxGroups)
            .InclusiveBetween(TournamentFormat.MinGroups, TournamentFormat.MaxGroupsLimit)
            .When(x => x.MaxGroups.HasValue).OverridePropertyName("maxGroups");
        RuleFor(x => x.MaxTeamsPerGroup)
            .InclusiveBetween(TournamentFormat.MinTeamsPerGroup, TournamentFormat.MaxTeamsPerGroupLimit)
            .When(x => x.MaxTeamsPerGroup.HasValue).OverridePropertyName("maxTeamsPerGroup");
    }
}
