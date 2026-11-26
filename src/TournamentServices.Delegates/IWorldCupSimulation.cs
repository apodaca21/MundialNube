using TournamentServices.Domain;

namespace TournamentServices.Delegates;

public interface IWorldCupSimulation
{
    Task<IReadOnlyList<Group>> GenerateGroupsAsync(string tournamentId, CancellationToken ct = default);
    Task<IReadOnlyList<Group>> FillGroupsAsync(string tournamentId, CancellationToken ct = default);
    Task<IReadOnlyList<Match>> GenerateMatchesAsync(string tournamentId, CancellationToken ct = default);
    Task<IReadOnlyList<Match>> SimulateGroupsAsync(string tournamentId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupStanding>> GetStandingsAsync(string tournamentId, CancellationToken ct = default);
    Task<TournamentChampion> PlayKnockoutAsync(string tournamentId, CancellationToken ct = default);
}

public interface IScoreGenerator
{
    Score Generate(bool allowDraw);
}

public sealed class RandomScoreGenerator : IScoreGenerator
{
    public Score Generate(bool allowDraw)
    {
        var home = Random.Shared.Next(0, 6);
        var visitor = Random.Shared.Next(0, 6);
        if (!allowDraw && home == visitor)
        {
            if (Random.Shared.Next(2) == 0) home++;
            else visitor++;
        }
        return new Score(home, visitor);
    }
}
