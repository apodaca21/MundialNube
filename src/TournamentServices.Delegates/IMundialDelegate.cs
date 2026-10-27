using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface IMundialDelegate
{
    Task<MundialSimulation> SimulateAsync(string tournamentId, CancellationToken cancellationToken = default);
}

public class MundialSimulation
{
    public required Tournament Tournament { get; init; }
    public required string ChampionTeamId { get; init; }
    public required string ChampionName { get; init; }
    public required string RunnerUpName { get; init; }
    public required string ThirdPlaceName { get; init; }
    public required IReadOnlyList<GroupStanding> Standings { get; init; }
    public required IReadOnlyList<Match> Matches { get; init; }
}
