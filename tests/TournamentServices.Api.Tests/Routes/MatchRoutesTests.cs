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
    public async Task Get_Matches_Returns200_WithEmptyArray()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/matches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadList<MatchDto>(response));
    }

    [Fact]
    public async Task Get_Matches_Returns200_WithMatchesAndTeamDetails()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/matches");
        var matches = await ReadList<MatchDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = Assert.Single(matches);
        Assert.Equal("Mexico", match.HomeTeam?.Name);
        Assert.Equal("Argentina", match.VisitorTeam?.Name);
    }

    [Fact]
    public async Task Get_MatchById_Returns200()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        var created = await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/matches/{created.Id}");
        var match = await Read<MatchDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, match.Id);
        Assert.False(match.IsCompleted);
    }

    [Fact]
    public async Task Get_MatchById_Returns404_WhenMissing()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/matches/missing-match");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_MatchById_Returns400_WhenIdIsInvalid()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{tournament.Id}/matches/bad_id");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_Match_Returns201_WithLocation()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(group.Id, home.Id, visitor.Id));
        var created = await Read<MatchDto>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/tournaments/{tournament.Id}/matches/{created.Id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateMatch_Returns422_WhenTeamsAreTheSame()
    {
        var tournament = await CreateTournament("World Cup");
        var team = await CreateTeam("Mexico");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(null, team.Id, team.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_Returns422_WhenTeamIsNotInTheGroup()
    {
        var (tournament, group, home, _) = await SeedGroupWithTeams();
        var outsider = await CreateTeam("Canada");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(group.Id, home.Id, outsider.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_Returns422_WhenTeamIsNotInTournament()
    {
        var tournament = await CreateTournament("World Cup");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto(null, home.Id, visitor.Id));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_Returns404_WhenGroupDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");

        var response = await Client.PostAsJsonAsync($"/tournaments/{tournament.Id}/matches",
            new CreateMatchDto("missing-group", home.Id, visitor.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Score_Returns200_AndComputesWinner()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        var match = await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/matches/{match.Id}/score",
            new UpdateScoreDto(2, 1));
        var scored = await Read<MatchDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("HOME", scored.Winner?.ToString());
        Assert.True(scored.IsCompleted);
    }

    [Fact]
    public async Task Patch_Score_Returns200_AndKeepsWinnerNullOnDraw()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        var match = await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/matches/{match.Id}/score",
            new UpdateScoreDto(1, 1));
        var scored = await Read<MatchDto>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(scored.Winner);
        Assert.True(scored.IsCompleted);
    }

    [Fact]
    public async Task Patch_Score_Returns400_WhenScoreIsNegative()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        var match = await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/matches/{match.Id}/score",
            new UpdateScoreDto(-1, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Score_Returns404_WhenMatchDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.PatchAsJsonAsync(
            $"/tournaments/{tournament.Id}/matches/missing-match/score",
            new UpdateScoreDto(1, 0));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Match_Returns204()
    {
        var (tournament, group, home, visitor) = await SeedGroupWithTeams();
        var match = await CreateMatch(tournament.Id, group.Id, home.Id, visitor.Id);

        var response = await Client.DeleteAsync($"/tournaments/{tournament.Id}/matches/{match.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Client.GetAsync($"/tournaments/{tournament.Id}/matches/{match.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_Match_Returns404_WhenMatchDoesNotExist()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.DeleteAsync($"/tournaments/{tournament.Id}/matches/missing-match");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Match_Returns400_WhenIdIsInvalid()
    {
        var tournament = await CreateTournament("World Cup");

        var response = await Client.DeleteAsync($"/tournaments/{tournament.Id}/matches/bad_id");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(TournamentDto Tournament, GroupDto Group, TeamDto Home, TeamDto Visitor)> SeedGroupWithTeams()
    {
        var tournament = await CreateTournament("World Cup");
        var home = await CreateTeam("Mexico");
        var visitor = await CreateTeam("Argentina");
        var group = await CreateGroup(tournament.Id, "Group A");
        await AssignTeams(tournament.Id, group.Id, [home.Id, visitor.Id]);
        return (tournament, group, home, visitor);
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

    private async Task<MatchDto> CreateMatch(string tournamentId, string groupId, string homeTeamId, string visitorTeamId)
    {
        var response = await Client.PostAsJsonAsync($"/tournaments/{tournamentId}/matches",
            new CreateMatchDto(groupId, homeTeamId, visitorTeamId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read<MatchDto>(response);
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
