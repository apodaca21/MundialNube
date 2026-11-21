using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests.Routes;

public class TournamentIntegrationTests : TournamentApiTests
{
    [Fact]
    public async Task GeneratedTeams_CannotJoinGroupsOrMatchesOfAnotherTournament()
    {
        var first = await CreateTournament(Client, "First cup");
        var second = await CreateTournament(Client, "Second cup");
        var ownTeams = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{first.Id}/teams"))!;
        var foreignTeams = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{second.Id}/teams"))!;
        var groupResponse = await Client.PostAsJsonAsync($"/tournaments/{first.Id}/groups", new { name = "Group A" });
        var group = (await groupResponse.Content.ReadFromJsonAsync<GroupDto>())!;

        var assignment = await Client.PatchAsJsonAsync($"/tournaments/{first.Id}/groups/{group.Id}/teams",
            new { teamIds = new[] { ownTeams[0].Id, foreignTeams[0].Id } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, assignment.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<GroupDto>($"/tournaments/{first.Id}/groups/{group.Id}"))!.Teams);

        var match = await Client.PostAsJsonAsync($"/tournaments/{first.Id}/matches", new
        {
            homeTeamId = ownTeams[0].Id, visitorTeamId = foreignTeams[0].Id
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, match.StatusCode);
        Assert.Empty((await Client.GetFromJsonAsync<List<MatchDto>>($"/tournaments/{first.Id}/matches"))!);
    }

    [Fact]
    public async Task GeneratedTeams_CanBeRenamedAssignedToGroupsAndUsedInMatches()
    {
        var tournament = await CreateTournament(Client, "Playable cup");
        var teams = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{tournament.Id}/teams"))!;
        var catalog = (await Client.GetFromJsonAsync<List<TeamDto>>("/teams"))!;
        Assert.Equal(32, catalog.Count);
        Assert.All(teams, team => Assert.Contains(catalog, item => item.Id == team.Id && item.Name == team.Name));

        var renamed = await Client.PutAsJsonAsync($"/teams/{teams[0].Id}", new { name = "Renamed champions" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Renamed champions", (await Client.GetFromJsonAsync<TeamDto>($"/teams/{teams[0].Id}"))!.Name);
        var updatedTeams = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{tournament.Id}/teams"))!;
        Assert.Equal("Renamed champions", updatedTeams.Single(team => team.Id == teams[0].Id).Name);

        var groupResponse = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/groups", new { name = "Group A" });
        Assert.Equal(HttpStatusCode.Created, groupResponse.StatusCode);
        var group = (await groupResponse.Content.ReadFromJsonAsync<GroupDto>())!;
        var assigned = await Client.PatchAsJsonAsync($"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new { teamIds = teams.Take(4).Select(team => team.Id).ToArray() });
        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        var groups = (await Client.GetFromJsonAsync<List<GroupDto>>($"/tournaments/{tournament.Id}/groups"))!;
        Assert.Equal(4, Assert.Single(groups).Teams.Count);
        Assert.Contains(groups[0].Teams, team => team.Name == "Renamed champions");

        var matchResponse = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches", new
        {
            groupId = group.Id, homeTeamId = teams[0].Id, visitorTeamId = teams[1].Id
        });
        Assert.Equal(HttpStatusCode.Created, matchResponse.StatusCode);
        var createdMatch = await matchResponse.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = createdMatch.GetProperty("id").GetString();
        var score = await Client.PatchAsJsonAsync($"/tournaments/{tournament.Id}/matches/{matchId}/score",
            new { homeTeamScore = 2, visitorTeamScore = 1 });
        Assert.Equal(HttpStatusCode.OK, score.StatusCode);
        var matches = await Client.GetFromJsonAsync<JsonElement>($"/tournaments/{tournament.Id}/matches");
        Assert.Equal("Renamed champions", matches[0].GetProperty("homeTeam").GetProperty("name").GetString());
        Assert.True(matches[0].GetProperty("isCompleted").GetBoolean());

        var completed = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/champion", new { teamId = teams[0].Id });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("Renamed champions", (await completed.Content.ReadFromJsonAsync<TournamentChampionDto>())!.TeamName);
    }

    [Fact]
    public async Task Metrics_ReflectCurrentDatabase_AndReplaceLatestChampionLabels()
    {
        var empty = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_tournaments_total 0", empty);
        Assert.Contains("mundial_champions_total 0", empty);
        Assert.Empty(Series(empty, "mundial_latest_champion_info"));

        var first = await CreateTournament(Client, "First cup");
        var firstTeam = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{first.Id}/teams"))![0];
        (await Client.PostAsJsonAsync($"/tournaments/{first.Id}/champion", new { teamId = firstTeam.Id })).EnsureSuccessStatusCode();
        var firstScrape = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_tournaments_total 1", firstScrape);
        Assert.Contains("mundial_champions_total 1", firstScrape);
        Assert.Contains("tournament_name=\"First cup\"", Assert.Single(Series(firstScrape, "mundial_latest_champion_info")));

        var second = await CreateTournament(Client, "Second cup");
        var secondTeam = (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{second.Id}/teams"))![0];
        (await Client.PostAsJsonAsync($"/tournaments/{second.Id}/champion", new { teamId = secondTeam.Id })).EnsureSuccessStatusCode();
        var secondScrape = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_tournaments_total 2", secondScrape);
        Assert.Contains("mundial_champions_total 2", secondScrape);
        var latest = Assert.Single(Series(secondScrape, "mundial_latest_champion_info"));
        Assert.Contains("tournament_name=\"Second cup\"", latest);
        Assert.DoesNotContain("First cup", latest);
        Assert.Equal(2, Series(secondScrape, "mundial_recent_champion_info").Length);
        (await Client.DeleteAsync($"/tournaments/{first.Id}")).EnsureSuccessStatusCode();
        var afterDeletion = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_tournaments_total 1", afterDeletion);
        Assert.Contains("mundial_champions_total 2", afterDeletion);
    }

    [Fact]
    public async Task Metrics_OnlyExpose20NewestHistorySeries_AndRemoveStaleSeries()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            db.Set<TournamentChampion>().AddRange(Enumerable.Range(1, 25).Select(number => new TournamentChampion
            {
                TournamentId = $"cup-{number}", TournamentName = $"Cup {number}",
                TeamId = $"winner-{number}", TeamName = $"Winner {number}",
                CompletedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(number)
            }));
            await db.SaveChangesAsync();
        }

        var scrape = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_champions_total 25", scrape);
        var history = Series(scrape, "mundial_recent_champion_info");
        Assert.Equal(20, history.Length);
        Assert.Contains(history, line => line.Contains("rank=\"1\"") && line.Contains("team_name=\"Winner 25\""));
        Assert.Contains(history, line => line.Contains("rank=\"20\"") && line.Contains("team_name=\"Winner 6\""));

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            await db.Set<TournamentChampion>().ExecuteDeleteAsync();
        }
        var cleared = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_champions_total 0", cleared);
        Assert.Empty(Series(cleared, "mundial_recent_champion_info"));
        Assert.Empty(Series(cleared, "mundial_latest_champion_info"));
    }

    [Fact]
    public async Task Restart_PreservesGeneratedTeamsChampionAndMetrics_WithoutGlobalRegistryLeakage()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"mundial-restart-{Guid.NewGuid():N}.db");
        string tournamentId;
        TournamentChampionDto champion;
        try
        {
            using (var factory = new FileDatabaseFactory(databasePath))
            using (var client = factory.CreateClient())
            {
                tournamentId = (await CreateTournament(client, "Restart cup")).Id;
                var teams = (await client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{tournamentId}/teams"))!;
                var completion = await client.PostAsJsonAsync($"/tournaments/{tournamentId}/champion", new { teamId = teams[0].Id });
                completion.EnsureSuccessStatusCode();
                champion = (await completion.Content.ReadFromJsonAsync<TournamentChampionDto>())!;
                Assert.Contains("mundial_champions_total 1", await client.GetStringAsync("/metrics"));
            }
            using (var restarted = new FileDatabaseFactory(databasePath))
            using (var client = restarted.CreateClient())
            {
                Assert.Equal(champion, await client.GetFromJsonAsync<TournamentChampionDto>("/reports/champions/latest"));
                Assert.Equal(32, (await client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{tournamentId}/teams"))!.Count);
                Assert.NotNull(await client.GetFromJsonAsync<TeamDto>($"/teams/{champion.TeamId}"));
                var scrape = await client.GetStringAsync("/metrics");
                Assert.Contains("mundial_tournaments_total 1", scrape);
                Assert.Contains("mundial_champions_total 1", scrape);
                Assert.Single(Series(scrape, "mundial_latest_champion_info"));
            }
            // The independent application created by this test's base factory remains empty.
            var isolated = await Client.GetStringAsync("/metrics");
            Assert.Contains("mundial_champions_total 0", isolated);
            Assert.Empty(Series(isolated, "mundial_latest_champion_info"));
        }
        finally
        {
            File.Delete(databasePath);
            File.Delete(databasePath + "-wal");
            File.Delete(databasePath + "-shm");
        }
    }

    private static string[] Series(string metrics, string name) => metrics.Split('\n')
        .Where(line => line.StartsWith(name + "{", StringComparison.Ordinal)).ToArray();

    private static async Task<TournamentDto> CreateTournament(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/tournaments", new
        {
            name, format = new { type = "ROUND_ROBIN", maxGroups = 8, maxTeamsPerGroup = 4 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>())!;
    }

    private sealed class FileDatabaseFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TournamentDbContext>();
                services.RemoveAll<DbContextOptions<TournamentDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<TournamentDbContext>>();
                services.AddDbContext<TournamentDbContext>(options =>
                    options.UseSqlite($"Data Source={databasePath};Foreign Keys=True;Pooling=False"));
            });
        }
    }
}
