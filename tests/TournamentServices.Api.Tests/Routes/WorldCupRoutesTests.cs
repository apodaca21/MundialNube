using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;
using TournamentServices.Delegates;
using TournamentServices.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace TournamentServices.Api.Tests.Routes;

public class WorldCupRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    [Fact]
    public async Task WorldCup_CompletesAll63Matches_AndRecordsChampionOnce()
    {
        var id = await Create();
        var groups = await Post<List<Group>>($"/tournaments/{id}/groups/generate");
        Assert.Equal(8, groups.Count);
        Assert.Equal(8, (await Post<List<Group>>($"/tournaments/{id}/groups/generate")).Count);
        await Post<object>($"/tournaments/{id}/groups/fill");
        await Post<object>($"/tournaments/{id}/groups/fill");
        var filled = (await Client.GetFromJsonAsync<List<GroupDto>>($"/tournaments/{id}/groups"))!;
        Assert.All(filled, group => Assert.Equal(4, group.Teams.Count));
        Assert.Equal(32, filled.SelectMany(group => group.Teams).Select(team => team.Id).Distinct().Count());
        await Post<object>($"/tournaments/{id}/matches/generate");
        await Post<object>($"/tournaments/{id}/matches/generate");
        var fixtures = (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!;
        Assert.Equal(48, fixtures.Count);
        Assert.All(fixtures, match => { Assert.False(match.IsCompleted); Assert.Equal(0, match.Round); });
        Assert.Empty((await Client.GetFromJsonAsync<List<TournamentChampionDto>>("/reports/champions"))!);
        await Post<object>($"/tournaments/{id}/matches/simulate");
        var simulated = (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!;
        await Post<object>($"/tournaments/{id}/matches/simulate");
        var repeated = (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!;
        Assert.Equal(simulated.OrderBy(match => match.Id).Select(match => match.Score),
            repeated.OrderBy(match => match.Id).Select(match => match.Score));
        var standings = (await Client.GetFromJsonAsync<List<GroupStanding>>($"/tournaments/{id}/standings"))!;
        Assert.Equal(8, standings.Count);
        Assert.All(standings.SelectMany(group => group.Teams), team => Assert.Equal(3, team.Played));
        Assert.Equal(standings.SelectMany(group => group.Teams).Sum(team => team.GoalsFor),
            standings.SelectMany(group => group.Teams).Sum(team => team.GoalsAgainst));
        var champion = await Post<TournamentChampionDto>($"/tournaments/{id}/knockout");
        Assert.Equal(champion, await Post<TournamentChampionDto>($"/tournaments/{id}/knockout"));
        Assert.Equal(champion, await Client.GetFromJsonAsync<TournamentChampionDto>("/reports/champions/latest"));
        var final = (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!;
        Assert.Equal(63, final.Count);
        Assert.All(final, match => Assert.True(match.IsCompleted));
        Assert.Equal(new[] { 8, 4, 2, 1 }, final.Where(match => match.Round > 0).GroupBy(match => match.Round)
            .OrderBy(round => round.Key).Select(round => round.Count()));
        Assert.All(final.Where(match => match.Round > 0), match =>
        {
            Assert.Null(match.GroupId); Assert.NotNull(match.Winner);
            Assert.NotEqual(match.Score.HomeTeamScore, match.Score.VisitorTeamScore);
        });
        var lastMatch = final.Single(match => match.Round == 4);
        Assert.Contains(champion.TeamId, new[] { lastMatch.HomeTeamId, lastMatch.VisitorTeamId });
        Assert.Single((await Client.GetFromJsonAsync<List<TournamentChampionDto>>("/reports/champions"))!);
    }

    [Fact]
    public async Task InvalidPhases_AreRejectedWithoutCreatingPartialFixtures()
    {
        var id = await Create();
        foreach (var phase in new[] { "groups/fill", "matches/generate", "matches/simulate", "knockout" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Client.PostAsync($"/tournaments/{id}/{phase}", null)).StatusCode);
        await Post<object>($"/tournaments/{id}/groups/generate");
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Client.PostAsync($"/tournaments/{id}/matches/generate", null)).StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!);
        await Post<object>($"/tournaments/{id}/groups/fill");
        await Post<object>($"/tournaments/{id}/matches/generate");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Client.PostAsync($"/tournaments/{id}/knockout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Client.PostAsync($"/tournaments/{id}/groups/fill", null)).StatusCode);
        Assert.Equal(48, (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!.Count);
    }

    [Fact]
    public async Task SimulationFailure_RollsBackScoresWrittenEarlierInTheSamePhase()
    {
        var id = await Create();
        await Post<object>($"/tournaments/{id}/groups/generate");
        await Post<object>($"/tournaments/{id}/groups/fill");
        await Post<object>($"/tournaments/{id}/matches/generate");
        await using (var scope = Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var simulation = new WorldCupSimulation(services.GetRequiredService<ITournamentRepository>(),
                services.GetRequiredService<IGroupRepository>(), services.GetRequiredService<IMatchRepository>(),
                services.GetRequiredService<ITeamRepository>(), services.GetRequiredService<ITournamentResultsRepository>(),
                services.GetRequiredService<IUnitOfWork>(), new FailingScoreGenerator());
            await Assert.ThrowsAsync<InvalidOperationException>(() => simulation.SimulateGroupsAsync(id));
        }
        var unchanged = (await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{id}/matches", Json))!;
        Assert.Equal(48, unchanged.Count);
        Assert.All(unchanged, match =>
        {
            Assert.False(match.IsCompleted);
            Assert.Equal(new ScoreDto(0, 0), match.Score);
        });
    }

    private sealed class FailingScoreGenerator : IScoreGenerator
    {
        private int calls;
        public Score Generate(bool allowDraw) => ++calls == 3
            ? throw new InvalidOperationException("Injected simulation failure") : new Score(3, 1);
    }

    [Fact]
    public async Task MissingAndMalformedTournamentIds_Return404And400()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsync("/tournaments/missing/knockout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/tournaments/invalid_id/standings")).StatusCode);
    }

    private async Task<string> Create()
    {
        var response = await Client.PostAsJsonAsync("/tournaments", new
        {
            name = "Simulation test", format = new { type = "ROUND_ROBIN", maxGroups = 8, maxTeamsPerGroup = 4 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>())!.Id;
    }
    private async Task<T> Post<T>(string url)
    {
        var response = await Client.PostAsync(url, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
