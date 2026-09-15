using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Tests.Routes;

public class MatchRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task CreateMatch_Returns422_WhenTeamsAreTheSame()
    {
        var tournament = await CreateTournament("World Cup");
        var team = await CreateTeam("Mexico");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(null, team.Id, team.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<TournamentDto> CreateTournament(string name, int maxTeamsPerGroup = 4)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", new
        {
            name,
            format = new { type = "ROUND_ROBIN", maxGroups = 4, maxTeamsPerGroup }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TournamentDto>(response);
    }

    private async Task<TeamDto> CreateTeam(string name)
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TeamDto>(response);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(value);
        return value;
    }
}