using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;

namespace TournamentServices.Api.Validators;

public class TournamentFormatDtoValidator : AbstractValidator<TournamentFormatDto>
{
    public TournamentFormatDtoValidator()
    {
        RuleFor(x => x.Type).Equal(TournamentFormat.RoundRobin).OverridePropertyName("type");
        RuleFor(x => x.RequiredGroups)
            .Equal(TournamentFormat.RequiredGroups)
            .OverridePropertyName("RequiredGroups");
        RuleFor(x => x.RequiredTeamsPerGroup)
            .Equal(TournamentFormat.RequiredTeamsPerGroup)
            .OverridePropertyName("RequiredTeamsPerGroup");
    }
}
