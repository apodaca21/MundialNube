using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;

namespace TournamentServices.Api.Validators;

public class TournamentFormatDtoValidator : AbstractValidator<TournamentFormatDto>
{
    public TournamentFormatDtoValidator()
    {
        RuleFor(x => x.Type).Equal(TournamentFormat.RoundRobin).OverridePropertyName("type");
        RuleFor(x => x.MaxGroups)
            .InclusiveBetween(TournamentFormat.MinGroups, TournamentFormat.MaxGroupsLimit)
            .OverridePropertyName("maxGroups");
        RuleFor(x => x.MaxTeamsPerGroup)
            .InclusiveBetween(TournamentFormat.MinTeamsPerGroup, TournamentFormat.MaxTeamsPerGroupLimit)
            .OverridePropertyName("maxTeamsPerGroup");
    }
}
