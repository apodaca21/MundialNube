using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Tests.Routes;

public class TeamRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Get_Teams_Returns200_WithEmptyArray()
    {
        var response = await Client.GetAsync("/teams");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadList(response));
    }

    [Fact]
    public async Task Get_Teams_Returns200_WithTeams()
    {
        await CreateTeam("Mexico");
        await CreateTeam("Argentina");

        var response = await Client.GetAsync("/teams");
        var teams = await ReadList(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, teams.Count);
        Assert.Contains(teams, t => t.Name == "Mexico");
        Assert.Contains(teams, t => t.Name == "Argentina");
    }

    [Fact]
    public async Task Get_TeamById_Returns200_WhenTeamExists()
    {
        var created = await CreateTeam("Mexico");

        var response = await Client.GetAsync($"/teams/{created.Id}");
        var team = await Read(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, team.Id);
        Assert.Equal("Mexico", team.Name);
    }

    [Fact]
    public async Task Get_TeamById_Returns404_WhenTeamDoesNotExist()
    {
        var response = await Client.GetAsync("/teams/missing-team-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_TeamById_Returns400_WhenIdFormatIsInvalid()
    {
        var response = await Client.GetAsync("/teams/bad_id");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Teams_Returns201_WithLocation()
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto("Mexico"));
        var team = await Read(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Mexico", team.Name);
        Assert.Matches(@"^[A-Za-z0-9\-]+$", team.Id);
        Assert.EndsWith($"/teams/{team.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_Teams_Returns400_WhenNameIsMissingEmptyOrDuplicate()
    {
        var missing = await Client.PostAsJsonAsync("/teams", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var empty = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(""));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        await Client.PostAsJsonAsync("/teams", new CreateTeamDto("Mexico"));
        var duplicate = await Client.PostAsJsonAsync("/teams", new CreateTeamDto("Mexico"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task Put_Team_Returns200_WhenUpdateIsValid()
    {
        var created = await CreateTeam("Mexico");

        var response = await Client.PutAsJsonAsync($"/teams/{created.Id}", new UpdateTeamDto("Argentina"));
        var updated = await Read(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Argentina", updated.Name);
    }

    [Fact]
    public async Task Put_Team_Returns404_WhenTeamDoesNotExist()
    {
        var response = await Client.PutAsJsonAsync("/teams/missing-team-id", new UpdateTeamDto("Argentina"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Team_Returns204_WhenTeamExists()
    {
        var created = await CreateTeam("Mexico");

        var response = await Client.DeleteAsync($"/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var lookup = await Client.GetAsync($"/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [Fact]
    public async Task Delete_Team_Returns404_WhenTeamDoesNotExist()
    {
        var response = await Client.DeleteAsync("/teams/missing-team-id");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<TeamDto> CreateTeam(string name)
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read(response);
    }

    private static async Task<TeamDto> Read(HttpResponseMessage response)
    {
        var team = await response.Content.ReadFromJsonAsync<TeamDto>(Json);
        Assert.NotNull(team);
        return team;
    }

    private static async Task<List<TeamDto>> ReadList(HttpResponseMessage response)
    {
        var teams = await response.Content.ReadFromJsonAsync<List<TeamDto>>(Json);
        Assert.NotNull(teams);
        return teams;
    }
}
