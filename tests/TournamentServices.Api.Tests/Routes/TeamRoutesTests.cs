using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Tests.Routes;

public class TeamRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Get_Teams_Returns200_WithEmptyArray()
    {
        var response = await Client.GetAsync("/teams");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var teams = await ReadTeamsAsync(response);
        Assert.Empty(teams);
    }

    [Fact]
    public async Task Get_Teams_Returns200_WithTeams()
    {
        await CreateTeamAsync("Mexico");
        await CreateTeamAsync("Argentina");

        var response = await Client.GetAsync("/teams");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var teams = await ReadTeamsAsync(response);
        Assert.Equal(2, teams.Count);
        Assert.Contains(teams, team => team.Name == "Mexico");
        Assert.Contains(teams, team => team.Name == "Argentina");
    }

    [Fact]
    public async Task Get_TeamById_Returns200_WhenTeamExists()
    {
        var created = await CreateTeamAsync("Mexico");

        var response = await Client.GetAsync($"/teams/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var team = await ReadTeamAsync(response);
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var team = await ReadTeamAsync(response);
        Assert.Equal("Mexico", team.Name);
        Assert.Matches(@"^[A-Za-z0-9\-]+$", team.Id);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/teams/{team.Id}", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Post_Teams_Returns400_WhenNameIsMissingEmptyOrDuplicate()
    {
        var missingName = await Client.PostAsJsonAsync("/teams", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);

        var emptyName = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(""));
        Assert.Equal(HttpStatusCode.BadRequest, emptyName.StatusCode);

        var created = await Client.PostAsJsonAsync("/teams", new CreateTeamDto("Mexico"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var duplicateName = await Client.PostAsJsonAsync("/teams", new CreateTeamDto("Mexico"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateName.StatusCode);
    }

    [Fact]
    public async Task Put_Team_Returns200_WhenUpdateIsValid()
    {
        var created = await CreateTeamAsync("Mexico");

        var response = await Client.PutAsJsonAsync($"/teams/{created.Id}", new UpdateTeamDto("Argentina"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadTeamAsync(response);
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
        var created = await CreateTeamAsync("Mexico");

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

    private async Task<TeamDto> CreateTeamAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadTeamAsync(response);
    }

    private static async Task<TeamDto> ReadTeamAsync(HttpResponseMessage response)
    {
        var team = await response.Content.ReadFromJsonAsync<TeamDto>(JsonOptions);
        Assert.NotNull(team);
        return team;
    }

    private static async Task<List<TeamDto>> ReadTeamsAsync(HttpResponseMessage response)
    {
        var teams = await response.Content.ReadFromJsonAsync<List<TeamDto>>(JsonOptions);
        Assert.NotNull(teams);
        return teams;
    }
}
