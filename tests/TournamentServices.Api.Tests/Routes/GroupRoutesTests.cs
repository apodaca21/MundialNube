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
    public async Task GroupWorkflow_CreatesAssignsScoresAndDeletesGroupMatches()
    {
        var tournament = await CreateTournament("World Cup", maxTeamsPerGroup: 4);
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

    [Fact]
    public async Task AssignTeams_Returns422_WhenTeamLimitIsExceeded()
    {
        var tournament = await CreateTournament("World Cup", maxTeamsPerGroup: 4);
        var group = await CreateGroup(tournament.Id, "Group A");
        var first = await CreateTeam("Mexico");
        var second = await CreateTeam("Argentina");
        var third = await CreateTeam("Canada");
        var forth=await CreateTeam("Colombia");
        var fith=await CreateTeam("USA");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/groups/{group.Id}/teams",
            new AssignTeamsDto([first.Id, second.Id, third.Id,forth.Id,fith.Id]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private async Task<TournamentDto> CreateTournament(string name, int maxTeamsPerGroup = 8)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", new
        {
            name,
            format = new { type = "ROUND_ROBIN", maxGroups = 8, maxTeamsPerGroup }
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

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        Assert.NotNull(value);
        return value;
    }
}