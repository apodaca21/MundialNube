using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Tests.Routes;

public class GroupRoutesTests : TournamentApiTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Get_Groups_Returns200_WithEmptyArray()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadList<GroupDto>(response));
    }

    [Fact]
    public async Task Get_Groups_Returns200_WithGroups()
    {
        var tournament = await CreateTournament("World Cup");
        await CreateGroup(tournament.Id, "Group A");
        await CreateGroup(tournament.Id, "Group B");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/groups");
        var groups = await ReadList<GroupDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public async Task Get_GroupById_Returns200_WithTeams()
    {
        var tournament = await CreateTournament("World Cup");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");
        var group = await CreateGroup(tournament.Id, "Group A");
        await AssignTeams(tournament.Id, group.Id, [home.Id, visitor.Id]);

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/groups/{group.Id}");
        var loaded = await Read<GroupDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, loaded.Teams.Count);
        Assert.Contains(loaded.Teams, team => team.Name == "Mexico");
    }

    [Fact]
    public async Task Get_GroupById_Returns404_WhenMissing()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/groups/missing-group");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_GroupById_Returns400_WhenIdIsInvalid()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/groups/bad_id");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Group_Returns201_WithLocation()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/groups", new CreateGroupDto("Group A"));
        var created = await Read<GroupDto>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Group A", created.Name);
        Assert.Equal($"/tournaments/{tournament.Id}/groups/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_Group_Returns404_WhenTournamentDoesNotExist()
    {
        var response = await Client.PostAsJsonAsync("/tournaments/missing-cup/groups", new CreateGroupDto("Group A"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Group_Returns400_WhenNameIsEmpty()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/groups", new CreateGroupDto(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Group_Returns422_WhenNameIsDuplicated()
    {
        var tournament = await CreateTournament("World Cup");
        await CreateGroup(tournament.Id, "Group A");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/groups", new CreateGroupDto("Group A"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Put_Group_Returns200_WhenUpdateIsValid()
    {
        var tournament = await CreateTournament("World Cup");
        var group = await CreateGroup(tournament.Id, "Group A");

        var response = await Client.PutAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}",
            new UpdateGroupDto("Group B"));
        var updated = await Read<GroupDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Group B", updated.Name);
    }

    [Fact]
    public async Task Put_Group_Returns404_WhenGroupDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.PutAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/missing-group",
            new UpdateGroupDto("Group B"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Group_Returns204_WhenGroupExists()
    {
        var tournament = await CreateTournament("World Cup");
        var group = await CreateGroup(tournament.Id, "Group A");

        var response = await Client.DeleteAsync($"/tournaments/{tournament.Id}/groups/{group.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/tournaments/{tournament.Id}/groups/{group.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Group_Returns404_WhenGroupDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.DeleteAsync($"/tournaments/{tournament.Id}/groups/missing-group");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeams_Returns204_WhenTeamsAreValid()
    {
        var tournament = await CreateTournament("World Cup");
        var group = await CreateGroup(tournament.Id, "Group A");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new AssignTeamsDto([home.Id, visitor.Id]));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeams_Returns422_WhenTeamDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");
        var group = await CreateGroup(tournament.Id, "Group A");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new AssignTeamsDto(["missing-team"]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeams_Returns422_WhenTeamIsAlreadyInAnotherGroup()
    {
        var tournament = await CreateTournament("World Cup");
        var first = await CreateGroup(tournament.Id, "Group A");
        var second = await CreateGroup(tournament.Id, "Group B");
        var team = await CreateTeam("Mexico");
        await AssignTeams(tournament.Id, first.Id, [team.Id]);

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{second.Id}/teams",
            new AssignTeamsDto([team.Id]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeams_Returns404_WhenGroupDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");
        var team = await CreateTeam("Mexico");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/missing-group/teams",
            new AssignTeamsDto([team.Id]));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignTeams_Returns422_WhenTeamLimitIsExceeded()
    {
        var tournament = await CreateTournament("World Cup");
        var group = await CreateGroup(tournament.Id, "Group A");
        var first = await CreateTeam("Mexico");
        var second = await CreateTeam("Argentina");
        var third = await CreateTeam("Canada");
        var forth = await CreateTeam("Colombia");
        var fith = await CreateTeam("USA");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new AssignTeamsDto([first.Id, second.Id, third.Id, forth.Id, fith.Id]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GroupWorkflow_CreatesAssignsScoresAndDeletesGroupMatches()
    {
        var tournament = await CreateTournament("World Cup");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");

        var groupResponse = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/groups", new CreateGroupDto("Group A"));
        Assert.Equal(HttpStatusCode.Created, groupResponse.StatusCode);
        var group = await Read<GroupDto>(groupResponse);

        var assignResponse = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new AssignTeamsDto([home.Id, visitor.Id]));
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var matchResponse = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(group.Id, home.Id, visitor.Id));
        Assert.Equal(HttpStatusCode.Created, matchResponse.StatusCode);
        var match = await Read<MatchDto>(matchResponse);

        var scoreResponse = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/matches/{match.Id}/score",
            new UpdateScoreDto(2, 1));
        var scored = await Read<MatchDto>(scoreResponse);

        Assert.Equal(HttpStatusCode.OK, scoreResponse.StatusCode);
        Assert.Equal("HOME", scored.Winner?.ToString());
        Assert.True(scored.IsCompleted);

        var deleteResponse = await Client.DeleteAsync($"/tournaments/{tournament.Id}/groups/{group.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/tournaments/{tournament.Id}/groups/{group.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/tournaments/{tournament.Id}/matches/{match.Id}")).StatusCode);
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

    private async Task<TeamDto> CreateTeam(string name)
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<TeamDto>(response);
    }

    private async Task<GroupDto> CreateGroup(string tournamentId, string name)
    {
        var response = await Client.PostAsJsonAsync($"/tournaments/{tournamentId}/groups", new CreateGroupDto(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<GroupDto>(response);
    }

    private async Task AssignTeams(string tournamentId, string groupId, IReadOnlyList<string> teamIds)
    {
        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournamentId}/groups/{groupId}/teams",
            new AssignTeamsDto(teamIds));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(value);
        return value;
    }

    private static async Task<List<T>> ReadList<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<List<T>>(Json);
        Assert.NotNull(value);
        return value;
    }
}
