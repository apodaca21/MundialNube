using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;

namespace TournamentServices.Api.Tests.Routes;

public class MundialRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Post_Mundial_Returns404_WhenTournamentIsMissing()
    {
        var response = await Client.PostAsync("/tournaments/missing-cup/mundial", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Mundial_Returns400_WhenIdIsInvalid()
    {
        var response = await Client.PostAsync("/tournaments/bad_id/mundial", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Mundial_BuildsGroupsMatchesAndBracket()
    {
        var tournament = await CreateTournament("Mundial carga");

        var response = await Client.PostAsync($"/tournaments/{tournament.Id}/mundial", null);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, payload);
        var result = await Read<MundialSimulationDto>(response);
        Assert.Equal(tournament.Id, result.TournamentId);
        Assert.False(string.IsNullOrWhiteSpace(result.ChampionName));
        Assert.False(string.IsNullOrWhiteSpace(result.RunnerUpName));
        Assert.False(string.IsNullOrWhiteSpace(result.ThirdPlaceName));
        Assert.Equal(32, result.Standings.Count);
        Assert.Equal(64, result.Matches.Count);
        Assert.Equal(48, result.Matches.Count(match => match.Stage == MatchStages.Group));
        Assert.Equal(8, result.Matches.Count(match => match.Stage == MatchStages.RoundOf16));
        Assert.Equal(4, result.Matches.Count(match => match.Stage == MatchStages.Quarterfinal));
        Assert.Equal(2, result.Matches.Count(match => match.Stage == MatchStages.Semifinal));
        Assert.Equal(1, result.Matches.Count(match => match.Stage == MatchStages.ThirdPlace));
        Assert.Equal(1, result.Matches.Count(match => match.Stage == MatchStages.Final));
        Assert.All(result.Matches, match => Assert.True(match.Score.HomeTeamScore >= 0));
        Assert.All(result.Matches.Where(match => match.Stage != MatchStages.Group),
            match => Assert.False(string.IsNullOrWhiteSpace(match.WinnerTeamName)));

        var groups = await Client.GetFromJsonAsync<List<GroupDto>>($"/tournaments/{tournament.Id}/groups", Json);
        Assert.NotNull(groups);
        Assert.Equal(8, groups.Count);
        Assert.All(groups, group => Assert.Equal(4, group.Teams.Count));
    }

    private async Task<TournamentDto> CreateTournament(string name)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", new
        {
            name,
            format = new { type = "FIFA", maxGroups = 8, maxTeamsPerGroup = 4 }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TournamentDto>(response);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(value);
        return value;
    }
}
