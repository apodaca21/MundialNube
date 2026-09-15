using FluentValidation;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Validators;

public class UpdateScoreDtoValidator : AbstractValidator<UpdateScoreDto>
{
    public UpdateScoreDtoValidator()
    {
        RuleFor(dto => dto.HomeTeamScore).GreaterThanOrEqualTo(0);
        RuleFor(dto => dto.VisitorTeamScore).GreaterThanOrEqualTo(0);
    }
}