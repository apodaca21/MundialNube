using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests.Routes;

public class TournamentResultRoutesTests : TournamentApiTests
{
    [Fact]
    public async Task NewTournaments_GetSeparatePersistentTeams_WithoutReplacingPreviousTeams()
    {
        var first = await CreateTournament("First cup");
        var firstTeams = await GetTeams(first.Id);
        var second = await CreateTournament("Second cup");
        var secondTeams = await GetTeams(second.Id);

        Assert.Equal(32, firstTeams.Count);
        Assert.Equal(32, secondTeams.Count);
        Assert.Equal(32, firstTeams.Select(team => team.Id).Distinct().Count());
        Assert.All(firstTeams, team => Assert.Equal(first.Id, team.TournamentId));
        Assert.All(secondTeams, team => Assert.Equal(second.Id, team.TournamentId));
        Assert.Empty(firstTeams.Select(team => team.Id).Intersect(secondTeams.Select(team => team.Id)));
        Assert.Equal(firstTeams, await GetTeams(first.Id));
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        Assert.Equal(64, await db.Set<TournamentTeam>().CountAsync());
    }

    [Fact]
    public async Task Reports_AreEmptyUntilAChampionIsRecorded()
    {
        await CreateTournament("Unfinished cup");

        Assert.Empty(await GetChampions());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/reports/champions/latest")).StatusCode);
        var csv = await Client.GetStringAsync("/reports/champions.csv");
        Assert.Equal("tournamentId,tournamentName,teamId,teamName,completedAtUtc\r\n", csv);
    }

    [Fact]
    public async Task CompletingTournament_PersistsChampion_AndRetriesDoNotDuplicateIt()
    {
        var tournament = await CreateTournament("Cup winner");
        var team = (await GetTeams(tournament.Id))[0];
        var before = DateTime.UtcNow;

        var response = await Complete(tournament.Id, team.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var champion = (await response.Content.ReadFromJsonAsync<TournamentChampionDto>())!;
        var retry = await Complete(tournament.Id, team.Id);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(champion, await retry.Content.ReadFromJsonAsync<TournamentChampionDto>());
        Assert.Equal(tournament.Id, champion.TournamentId);
        Assert.Equal(tournament.Name, champion.TournamentName);
        Assert.Equal(team.Id, champion.TeamId);
        Assert.Equal(team.Name, champion.TeamName);
        Assert.InRange(champion.CompletedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, champion.CompletedAtUtc.Kind);
        Assert.Equal(champion, Assert.Single(await GetChampions()));
        Assert.Equal(champion, await Client.GetFromJsonAsync<TournamentChampionDto>("/reports/champions/latest"));
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        Assert.Equal(team.Id, (await db.Set<TournamentChampion>().SingleAsync()).TeamId);
    }

    [Fact]
    public async Task ChampionMustBelongToTournament_AndCannotBeReplaced()
    {
        var tournament = await CreateTournament("Original cup");
        var other = await CreateTournament("Other cup");
        var teams = await GetTeams(tournament.Id);
        var foreignTeam = (await GetTeams(other.Id))[0];

        Assert.Equal(HttpStatusCode.BadRequest, (await Complete(tournament.Id, foreignTeam.Id)).StatusCode);
        Assert.Empty(await GetChampions());
        Assert.Equal(HttpStatusCode.OK, (await Complete(tournament.Id, teams[0].Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Complete(tournament.Id, teams[1].Id)).StatusCode);
        Assert.Equal(teams[0].Id, Assert.Single(await GetChampions()).TeamId);
    }

    [Fact]
    public async Task ChampionSnapshot_RemainsAfterTournamentRenameAndDeletion()
    {
        var tournament = await CreateTournament("Historic name");
        var team = (await GetTeams(tournament.Id))[0];
        await Complete(tournament.Id, team.Id);

        var renamed = await Client.PatchAsJsonAsync($"/tournaments/{tournament.Id}", new { name = "New name" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Historic name", Assert.Single(await GetChampions()).TournamentName);

        var deleted = await Client.DeleteAsync($"/tournaments/{tournament.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var historic = Assert.Single(await GetChampions());
        Assert.Equal("Historic name", historic.TournamentName);
        Assert.Equal(team.Name, historic.TeamName);
    }

    [Fact]
    public async Task Report_ReturnsOnly20NewestCompletions_WithDeterministicTies()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            // Insertion order differs from completion order; the last two dates tie.
            foreach (var number in Enumerable.Range(1, 23).Reverse())
            {
                db.Set<TournamentChampion>().Add(new TournamentChampion
                {
                    TournamentId = $"tournament-{number}", TournamentName = $"Cup {number}",
                    TeamId = $"team-{number}", TeamName = $"Winner {number}",
                    CompletedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(Math.Min(number, 22))
                });
                await db.SaveChangesAsync();
            }
        }
        await CreateTournament("Unfinished cup");

        var report = await GetChampions();
        Assert.Equal(20, report.Count);
        Assert.Equal("tournament-22", report[0].TournamentId);
        Assert.Equal("tournament-23", report[1].TournamentId);
        Assert.Equal("tournament-4", report[^1].TournamentId);
        Assert.Equal(report.OrderByDescending(item => item.CompletedAtUtc).ThenByDescending(item => item.Id), report);
        Assert.Equal(report[0], await Client.GetFromJsonAsync<TournamentChampionDto>("/reports/champions/latest"));
        var csvLines = (await Client.GetStringAsync("/reports/champions.csv"))
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(21, csvLines.Length);
        Assert.Contains("tournament-22", csvLines[1]);
        Assert.Contains("tournament-4", csvLines[^1]);
    }

    [Fact]
    public async Task CsvReport_EscapesNamesAndPreventsSpreadsheetFormulaExecution()
    {
        var tournament = await CreateTournament("=SUM(\"1\",\"2\")\r\nCup");
        var team = (await GetTeams(tournament.Id))[0];
        await Complete(tournament.Id, team.Id);

        var response = await Client.GetAsync("/reports/champions.csv");
        var csv = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("ultimos-20-campeones.csv", response.Content.Headers.ContentDisposition?.ToString());
        Assert.Contains("\"'=SUM(\"\"1\"\",\"\"2\"\")\r\nCup\"", csv);
    }

    [Theory]
    [InlineData("/tournaments/missing/teams", HttpStatusCode.NotFound)]
    [InlineData("/tournaments/bad_id/teams", HttpStatusCode.BadRequest)]
    public async Task TeamList_ValidatesTournamentId(string path, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await Client.GetAsync(path)).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"teamId\":null}")]
    [InlineData("{\"teamId\":\"\"}")]
    [InlineData("{\"teamId\":\"bad_id\"}")]
    [InlineData("{\"teamId\":\"team-1\",\"completedAtUtc\":\"2000-01-01\"}")]
    public async Task Completion_RejectsInvalidBody_WithoutRecordingChampion(string body)
    {
        var tournament = await CreateTournament("Cup");
        var response = await Client.PostAsync($"/tournaments/{tournament.Id}/champion",
            new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await GetChampions());
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("bad_id", HttpStatusCode.BadRequest)]
    public async Task Completion_ValidatesTournamentId(string tournamentId, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await Complete(tournamentId, "team-1")).StatusCode);
    }

    private async Task<TournamentDto> CreateTournament(string name)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", new
        {
            name, format = new { type = "ROUND_ROBIN", maxGroups = 8, maxTeamsPerGroup = 4 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TournamentDto>())!;
    }

    private async Task<List<TournamentTeamDto>> GetTeams(string tournamentId) =>
        (await Client.GetFromJsonAsync<List<TournamentTeamDto>>($"/tournaments/{tournamentId}/teams"))!;

    private async Task<List<TournamentChampionDto>> GetChampions() =>
        (await Client.GetFromJsonAsync<List<TournamentChampionDto>>("/reports/champions"))!;

    private Task<HttpResponseMessage> Complete(string tournamentId, string teamId) =>
        Client.PostAsJsonAsync($"/tournaments/{tournamentId}/champion", new { teamId });
}
