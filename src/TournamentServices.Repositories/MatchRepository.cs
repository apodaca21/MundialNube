using System.Collections.Concurrent;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly ConcurrentDictionary<string, Match> _matches = new();

    public Task<IReadOnlyList<Match>> GetByTournamentIdAsync(string tournamentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Match>>(_matches.Values
            .Where(match => match.TournamentId == tournamentId)
            .OrderBy(match => match.Id)
            .Select(Copy)
            .ToList());

    public Task<Match?> GetByIdAsync(string tournamentId, string matchId, CancellationToken cancellationToken = default)
        => Task.FromResult(_matches.TryGetValue(matchId, out var match) && match.TournamentId == tournamentId
            ? Copy(match)
            : null);

    public Task<IReadOnlyList<Match>> GetByGroupIdAsync(string groupId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Match>>(_matches.Values
            .Where(match => match.GroupId == groupId)
            .OrderBy(match => match.Id)
            .Select(Copy)
            .ToList());

    public Task AddAsync(Match match, CancellationToken cancellationToken = default)
    {
        if (!_matches.TryAdd(match.Id, Copy(match)))
            throw new InvalidOperationException($"A match with id '{match.Id}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Match match, CancellationToken cancellationToken = default)
    {
        if (!_matches.ContainsKey(match.Id))
            throw new KeyNotFoundException($"Match '{match.Id}' was not found.");

        _matches[match.Id] = Copy(match);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string matchId, CancellationToken cancellationToken = default)
    {
        _matches.TryRemove(matchId, out _);
        return Task.CompletedTask;
    }

    private static Match Copy(Match match)
    {
        var copy = new Match(match.Id, match.TournamentId, match.GroupId, match.HomeTeamId, match.VisitorTeamId);
        if (match.IsCompleted)
            copy.UpdateScore(new Score(match.Score.HomeTeamScore, match.Score.VisitorTeamScore));

        return copy;
    }
}