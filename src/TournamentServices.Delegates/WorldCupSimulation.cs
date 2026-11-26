using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public sealed class WorldCupSimulation(
    ITournamentRepository tournaments, IGroupRepository groups, IMatchRepository matches,
    ITeamRepository teams, ITournamentResultsRepository results, IUnitOfWork unitOfWork,
    IScoreGenerator scoreGenerator) : IWorldCupSimulation
{
    private static readonly string[] Selections = ["Argentina", "Brasil", "Alemania", "Francia", "España", "Italia",
        "Uruguay", "México", "Portugal", "Países Bajos", "Croacia", "Colombia", "Japón", "Marruecos", "Bélgica",
        "Inglaterra", "Chile", "Costa Rica", "Estados Unidos", "Canadá", "Ecuador", "Paraguay", "Perú", "Suiza",
        "Dinamarca", "Suecia", "Polonia", "Senegal", "Corea del Sur", "Australia", "Nigeria", "Ghana"];

    public Task<IReadOnlyList<Group>> GenerateGroupsAsync(string tournamentId, CancellationToken ct = default)
        => Mutate(tournamentId, async tournament =>
        {
            await EnsureOpen(tournamentId, ct);
            await EnsureNoMatches(tournamentId, ct);
            var existing = (await groups.GetByTournamentIdAsync(tournamentId, ct)).ToList();
            if (existing.Count > tournament.Format.MaxGroups)
                throw Rule("The tournament already exceeds its group limit.");
            for (var index = 0; existing.Count < tournament.Format.MaxGroups; index++)
            {
                var name = $"Grupo {(char)('A' + index)}";
                if (existing.Any(group => group.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                var group = new Group(Guid.NewGuid().ToString(), name, tournamentId);
                await groups.AddAsync(group, ct);
                existing.Add(group);
            }
            return (IReadOnlyList<Group>)existing.OrderBy(group => group.Name).ToList();
        }, ct);

    public Task<IReadOnlyList<Group>> FillGroupsAsync(string tournamentId, CancellationToken ct = default)
        => Mutate(tournamentId, async tournament =>
        {
            await EnsureOpen(tournamentId, ct);
            await EnsureNoMatches(tournamentId, ct);
            var allGroups = (await groups.GetByTournamentIdAsync(tournamentId, ct)).OrderBy(group => group.Name).ToList();
            if (allGroups.Count != tournament.Format.MaxGroups)
                throw Rule("Generate all groups before filling them.");
            var participants = (await results.GetTeamsAsync(tournamentId, ct))!;
            if (participants.Count != tournament.Format.MaxGroups * tournament.Format.MaxTeamsPerGroup)
                throw Rule("The tournament does not have the required number of generated participants.");
            var ids = participants.Select(team => team.Id).ToHashSet();
            var assigned = allGroups.SelectMany(group => group.TeamIds).ToList();
            if (assigned.Distinct().Count() != assigned.Count || assigned.Any(id => !ids.Contains(id)) ||
                allGroups.Any(group => group.TeamIds.Count > tournament.Format.MaxTeamsPerGroup))
                throw Rule("Groups contain duplicate, foreign or excess participants.");
            // Distinct team IDs are retained. Only generated placeholder names receive selection labels.
            for (var index = 0; index < participants.Count; index++)
            {
                var participant = participants[index];
                if (!participant.Name.StartsWith("Equipo ", StringComparison.Ordinal)) continue;
                var name = index < Selections.Length ? Selections[index] : $"Equipo {index + 1}";
                await teams.UpdateAsync(new Team { Id = participant.Id, TournamentId = tournamentId,
                    Name = $"{name} - {tournamentId}" }, ct);
            }
            var unused = new Queue<string>(participants.Where(team => !assigned.Contains(team.Id)).Select(team => team.Id));
            foreach (var group in allGroups)
            {
                var members = group.TeamIds.ToList();
                while (members.Count < tournament.Format.MaxTeamsPerGroup) members.Add(unused.Dequeue());
                group.AssignTeams(members);
                await groups.UpdateAsync(group, ct);
            }
            return (IReadOnlyList<Group>)allGroups;
        }, ct);

    public Task<IReadOnlyList<Match>> GenerateMatchesAsync(string tournamentId, CancellationToken ct = default)
        => Mutate(tournamentId, async tournament =>
        {
            await EnsureOpen(tournamentId, ct);
            var allGroups = await FullGroups(tournament, ct);
            var existing = (await matches.GetByTournamentIdAsync(tournamentId, ct)).ToList();
            if (existing.Any(match => match.Round > 0)) throw Rule("Knockout has already started.");
            foreach (var group in allGroups)
            {
                var groupMatches = existing.Where(match => match.GroupId == group.Id && match.Round == 0).ToList();
                ValidateFixtures(group, groupMatches);
                for (var home = 0; home < group.TeamIds.Count; home++)
                for (var visitor = home + 1; visitor < group.TeamIds.Count; visitor++)
                {
                    var homeId = group.TeamIds[home];
                    var visitorId = group.TeamIds[visitor];
                    if (groupMatches.Any(match => SamePair(match, homeId, visitorId))) continue;
                    var match = new Match(Guid.NewGuid().ToString(), tournamentId, group.Id, homeId, visitorId);
                    await matches.AddAsync(match, ct);
                    existing.Add(match);
                }
            }
            return (IReadOnlyList<Match>)existing.Where(match => match.Round == 0).ToList();
        }, ct);

    public Task<IReadOnlyList<Match>> SimulateGroupsAsync(string tournamentId, CancellationToken ct = default)
        => Mutate(tournamentId, async tournament =>
        {
            await EnsureOpen(tournamentId, ct);
            var allGroups = await FullGroups(tournament, ct);
            var allMatches = await matches.GetByTournamentIdAsync(tournamentId, ct);
            if (allMatches.Any(match => match.Round > 0)) throw Rule("Knockout has already started.");
            ValidateCompleteSchedule(allGroups, allMatches);
            foreach (var match in allMatches.Where(match => match.Round == 0 && !match.IsCompleted))
            {
                match.UpdateScore(scoreGenerator.Generate(allowDraw: true));
                await matches.UpdateAsync(match, ct);
            }
            return (IReadOnlyList<Match>)allMatches.Where(match => match.Round == 0).ToList();
        }, ct);

    public async Task<IReadOnlyList<GroupStanding>> GetStandingsAsync(string tournamentId, CancellationToken ct = default)
    {
        await GetTournament(tournamentId, ct);
        var allGroups = await groups.GetByTournamentIdAsync(tournamentId, ct);
        var allMatches = await matches.GetByTournamentIdAsync(tournamentId, ct);
        var standings = new List<GroupStanding>();
        foreach (var group in allGroups.OrderBy(group => group.Name))
        {
            var members = new List<Team>();
            foreach (var id in group.TeamIds)
                members.Add(await teams.GetByIdAsync(id, ct) ?? throw Rule("A group participant no longer exists."));
            standings.Add(new GroupStanding(group.Id, group.Name,
                WorldCupRules.Standings(members, allMatches.Where(match => match.GroupId == group.Id))));
        }
        return standings;
    }

    public Task<TournamentChampion> PlayKnockoutAsync(string tournamentId, CancellationToken ct = default)
        => Mutate(tournamentId, async tournament =>
        {
            var previous = await results.GetChampionAsync(tournamentId, ct);
            if (previous is not null) return previous;
            var allGroups = await FullGroups(tournament, ct);
            var allMatches = await matches.GetByTournamentIdAsync(tournamentId, ct);
            ValidateCompleteSchedule(allGroups, allMatches);
            if (allMatches.Any(match => match.Round == 0 && !match.IsCompleted))
                throw Rule("All group matches must be completed before knockout.");
            if (allMatches.Any(match => match.Round > 0))
                throw Rule("Incomplete existing knockout detected; results cannot be regenerated.");
            var standings = await GetStandingsAsync(tournamentId, ct);
            var seeds = WorldCupRules.Order(standings.SelectMany(group => group.Teams.Take(2)))
                .Select(team => team.TeamId).ToList();
            if (seeds.Count < 2) throw Rule("At least two qualifiers are required.");
            var active = seeds.ToList();
            var round = 1;
            while (active.Count > 1)
            {
                var pairing = WorldCupRules.Pair(active);
                var advancing = pairing.Byes.ToList();
                foreach (var pair in pairing.Matches)
                {
                    var match = new Match(Guid.NewGuid().ToString(), tournamentId, null,
                        pair.HomeTeamId, pair.VisitorTeamId, round);
                    match.UpdateScore(scoreGenerator.Generate(allowDraw: false));
                    if (match.Winner is null) throw Rule("A knockout result must have a winner.");
                    await matches.AddAsync(match, ct);
                    advancing.Add(match.Score.HomeTeamScore > match.Score.VisitorTeamScore
                        ? match.HomeTeamId : match.VisitorTeamId);
                }
                active = seeds.Where(advancing.Contains).ToList();
                round++;
            }
            return await results.CompleteAsync(tournamentId, active.Single(), ct);
        }, ct);

    private async Task<T> Mutate<T>(string id, Func<Tournament, Task<T>> action, CancellationToken ct)
    {
        await unitOfWork.BeginAsync(id, ct);
        try
        {
            var result = await action(await GetTournament(id, ct));
            await unitOfWork.CommitAsync(ct);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Tournament> GetTournament(string id, CancellationToken ct)
        => await tournaments.GetByIdAsync(id, ct) ?? throw new TournamentNotFoundException(id);
    private async Task EnsureOpen(string id, CancellationToken ct)
    {
        if (await results.GetChampionAsync(id, ct) is not null) throw Rule("The tournament already has a champion.");
    }
    private async Task EnsureNoMatches(string id, CancellationToken ct)
    {
        if ((await matches.GetByTournamentIdAsync(id, ct)).Count > 0)
            throw Rule("Groups cannot be changed after fixtures have been created.");
    }
    private async Task<IReadOnlyList<Group>> FullGroups(Tournament tournament, CancellationToken ct)
    {
        var allGroups = await groups.GetByTournamentIdAsync(tournament.Id, ct);
        var participants = (await results.GetTeamsAsync(tournament.Id, ct))!.Select(team => team.Id).ToHashSet();
        var assigned = allGroups.SelectMany(group => group.TeamIds).ToList();
        if (allGroups.Count != tournament.Format.MaxGroups ||
            allGroups.Any(group => group.TeamIds.Count != tournament.Format.MaxTeamsPerGroup) ||
            assigned.Count != assigned.Distinct().Count() || assigned.Any(id => !participants.Contains(id)))
            throw Rule("Every group must be full with distinct participants from this tournament.");
        return allGroups;
    }
    private static void ValidateCompleteSchedule(IReadOnlyList<Group> groups, IReadOnlyList<Match> matches)
    {
        if (matches.Any(match => match.Round == 0 && (match.GroupId is null || groups.All(group => group.Id != match.GroupId))))
            throw Rule("Group fixtures contain a match outside the tournament groups.");
        foreach (var group in groups)
        {
            var fixtures = matches.Where(match => match.Round == 0 && match.GroupId == group.Id).ToList();
            ValidateFixtures(group, fixtures);
            if (fixtures.Count != group.TeamIds.Count * (group.TeamIds.Count - 1) / 2)
                throw Rule("Generate every group fixture before simulating or playing knockout.");
        }
    }
    private static void ValidateFixtures(Group group, IReadOnlyList<Match> fixtures)
    {
        if (fixtures.Any(match => match.HomeTeamId == match.VisitorTeamId ||
            !group.TeamIds.Contains(match.HomeTeamId) || !group.TeamIds.Contains(match.VisitorTeamId)))
            throw Rule("A group fixture contains invalid participants.");
        var pairs = fixtures.Select(match => string.CompareOrdinal(match.HomeTeamId, match.VisitorTeamId) < 0
            ? (match.HomeTeamId, match.VisitorTeamId) : (match.VisitorTeamId, match.HomeTeamId));
        if (pairs.Distinct().Count() != fixtures.Count) throw Rule("Group fixtures contain duplicate pairings.");
    }
    private static bool SamePair(Match match, string one, string two) =>
        match.HomeTeamId == one && match.VisitorTeamId == two || match.HomeTeamId == two && match.VisitorTeamId == one;
    private static SimulationRuleException Rule(string message) => new(message);
}
