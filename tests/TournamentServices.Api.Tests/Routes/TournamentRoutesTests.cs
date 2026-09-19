using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TournamentServices.Api.Dtos;
using TournamentServices.Domain;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests.Routes;

public class TournamentRoutesTests : TournamentApiTests
{
    [Fact]
    public async Task Get_Tournaments_Returns200_WithEmptyArray()
    {
        var response = await Client.GetAsync("/tournaments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ReadList(response));
    }

    [Fact]
    public async Task Get_Tournaments_Returns200_WithStoredTournamentsAndFormats()
    {
        var first = await CreateTournament("World Cup");
        var second = await CreateTournament("Regional Cup", 8, 4);

        var response = await Client.GetAsync("/tournaments");
        var tournaments = await ReadList(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, tournaments.Count);
        Assert.Contains(tournaments, tournament => tournament == first);
        Assert.Contains(tournaments, tournament => tournament == second);
    }

    [Fact]
    public async Task Get_TournamentById_Returns200_WithStoredFormat()
    {
        var created = await CreateTournament("World Cup");

        var response = await Client.GetAsync($"/tournaments/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await Read(response));
    }

    [Fact]
    public async Task Get_TournamentById_Returns404_WhenTournamentDoesNotExist()
    {
        var response = await Client.GetAsync("/tournaments/missing-tournament");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("bad_id")]
    [InlineData("bad%20id")]
    public async Task Get_TournamentById_Returns400_WhenIdFormatIsInvalid(string id)
    {
        var response = await Client.GetAsync($"/tournaments/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    //[InlineData(8, 4)]
    [InlineData(8, 4)]
    public async Task Post_Tournaments_Returns201_WithLocationAndPersistsFormat(int maxGroups, int maxTeamsPerGroup)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", FullBody("World Cup", maxGroups, maxTeamsPerGroup));
        var created = await Read(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Matches(Tournament.IdPattern, created.Id);
        Assert.Equal("World Cup", created.Name);
        Assert.Equal(new TournamentFormatDto("ROUND_ROBIN", maxGroups, maxTeamsPerGroup), created.Format);
        Assert.Equal($"/tournaments/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal(created, await GetTournament(created.Id));
    }

    [Theory]
    [MemberData(nameof(InvalidFullBodies))]
    public async Task Post_Tournaments_Returns400_AndDoesNotCreate_WhenBodyIsInvalid(string body)
    {
        var response = await SendJson(HttpMethod.Post, "/tournaments", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ReadList(await Client.GetAsync("/tournaments")));
    }

    [Fact]
    public async Task Put_Tournament_Returns200_AndReplacesNameAndEntireFormat()
    {
        var created = await CreateTournament("World Cup");

        var response = await Client.PutAsJsonAsync($"/tournaments/{created.Id}", FullBody("Regional Cup", 8, 4));
        var updated = await Read(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Regional Cup", updated.Name);
        Assert.Equal(new TournamentFormatDto("ROUND_ROBIN", 8, 4), updated.Format);
        Assert.Equal(updated, await GetTournament(created.Id));
    }

    [Fact]
    public async Task Put_Tournament_Returns404_WhenTournamentDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await Client.PutAsJsonAsync($"/tournaments/{nonExistentId}", FullBody("World Cup"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_Tournament_Returns400_WhenIdFormatIsInvalid()
    {
        var response = await Client.PutAsJsonAsync("/tournaments/bad_id", FullBody("World Cup"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidFullBodies))]
    public async Task Put_Tournament_Returns400_AndDoesNotModify_WhenBodyIsInvalid(string body)
    {
        var created = await CreateTournament("World Cup");

        var response = await SendJson(HttpMethod.Put, $"/tournaments/{created.Id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(created, await GetTournament(created.Id));
    }

    [Theory]
    [InlineData("{\"name\":\"Regional Cup\"}", "Regional Cup", 8, 4)]
    [InlineData("{\"format\":{\"type\":\"ROUND_ROBIN\"}}", "World Cup", 8, 4)]
    [InlineData("{\"format\":{\"maxGroups\":8}}", "World Cup", 8, 4)]
    [InlineData("{\"format\":{\"maxTeamsPerGroup\":4}}", "World Cup", 8, 4)]
    [InlineData("{\"name\":\"Regional Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":8,\"maxTeamsPerGroup\":4}}", "Regional Cup", 8, 4)]
    [InlineData("{\"name\":null,\"format\":{\"maxGroups\":8}}", "World Cup", 8, 4)]
    [InlineData("{\"name\":\"Regional Cup\",\"format\":null}", "Regional Cup", 8, 4)]
    [InlineData("{\"name\":\"Regional Cup\",\"format\":{}}", "Regional Cup", 8, 4)]
    [InlineData("{\"format\":{\"type\":null,\"maxGroups\":8,\"maxTeamsPerGroup\":null}}", "World Cup", 8, 4)]
    [InlineData("{\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":null,\"maxTeamsPerGroup\":null}}", "World Cup", 8, 4)]
    public async Task Patch_Tournament_Returns200_AndPreservesOmittedOrNullFields(
        string body, string expectedName, int expectedGroups, int expectedTeams)
    {
        var created = await CreateTournament("World Cup",maxGroups: 8);

        var response = await SendJson(HttpMethod.Patch, $"/tournaments/{created.Id}", body);
        var updated = await Read(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(expectedName, updated.Name);
        Assert.Equal(new TournamentFormatDto("ROUND_ROBIN", expectedGroups, expectedTeams), updated.Format);
        Assert.Equal(updated, await GetTournament(created.Id));
    }

    [Fact]
    public async Task Patch_Tournament_Returns404_WhenTournamentDoesNotExist()
    {
        var response = await SendJson(HttpMethod.Patch, "/tournaments/missing-tournament", "{\"name\":\"Regional Cup\"}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_Tournament_Returns400_WhenIdFormatIsInvalid()
    {
        var response = await SendJson(HttpMethod.Patch, "/tournaments/bad_id", "{\"name\":\"Regional Cup\"}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(InvalidPatchBodies))]
    public async Task Patch_Tournament_Returns400_AndDoesNotModify_WhenBodyIsInvalid(string body)
    {
        var created = await CreateTournament("World Cup");

        var response = await SendJson(HttpMethod.Patch, $"/tournaments/{created.Id}", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(created, await GetTournament(created.Id));
    }

    [Fact]
    public async Task Delete_Tournament_Returns204_AndCascadesGroupsAndMatches_WithoutAffectingAnotherTournament()
    {
        var deleted = await CreateTournament("World Cup");
        var retained = await CreateTournament("Regional Cup");

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
            db.Groups.AddRange(
                new TournamentGroup { Id = "group-delete", TournamentId = deleted.Id, Name = "Group A" },
                new TournamentGroup { Id = "group-keep", TournamentId = retained.Id, Name = "Group B" });
            db.Matches.AddRange(
                new TournamentMatch { Id = "match-delete", GroupId = "group-delete" },
                new TournamentMatch { Id = "match-keep", GroupId = "group-keep" });
            await db.SaveChangesAsync();
        }

        var response = await Client.DeleteAsync($"/tournaments/{deleted.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/tournaments/{deleted.Id}")).StatusCode);
        Assert.Equal(retained, await GetTournament(retained.Id));

        using var verificationScope = Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        Assert.Equal(retained.Id, (await verificationDb.Tournaments.SingleAsync()).Id);
        Assert.Equal("group-keep", (await verificationDb.Groups.SingleAsync()).Id);
        Assert.Equal("match-keep", (await verificationDb.Matches.SingleAsync()).Id);
    }

    [Fact]
    public async Task Delete_Tournament_Returns404_WhenTournamentDoesNotExist()
    {
        var response = await Client.DeleteAsync("/tournaments/missing-tournament");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Tournament_Returns400_WhenIdFormatIsInvalid()
    {
        var response = await Client.DeleteAsync("/tournaments/bad_id");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<string> InvalidFullBodies => new()
    {
        "{}",
        "{\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":null,\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"   \",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\"}",
        "{\"name\":\"World Cup\",\"format\":null}",
        "{\"name\":\"World Cup\",\"format\":{}}",
        "{\"name\":\"World Cup\",\"format\":{\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"KNOCKOUT\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"round_robin\",\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":null,\"maxGroups\":4,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":0,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":17,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":1}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":9}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":null,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4.5,\"maxTeamsPerGroup\":4}}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4},\"unknown\":true}",
        "{\"name\":\"World Cup\",\"format\":{\"type\":\"ROUND_ROBIN\",\"maxGroups\":4,\"maxTeamsPerGroup\":4,\"unknown\":true}}"
    };

    public static TheoryData<string> InvalidPatchBodies => new()
    {
        "{}",
        "{\"name\":null}",
        "{\"format\":null}",
        "{\"name\":null,\"format\":null}",
        "{\"format\":{}}",
        "{\"format\":{\"type\":null,\"maxGroups\":null,\"maxTeamsPerGroup\":null}}",
        "{\"name\":\"\"}",
        "{\"name\":\"   \"}",
        "{\"format\":{\"type\":\"\"}}",
        "{\"format\":{\"type\":\"KNOCKOUT\"}}",
        "{\"format\":{\"type\":\"round_robin\"}}",
        "{\"format\":{\"maxGroups\":0}}",
        "{\"format\":{\"maxGroups\":17}}",
        "{\"format\":{\"maxTeamsPerGroup\":1}}",
        "{\"format\":{\"maxTeamsPerGroup\":9}}",
        "{\"format\":{\"maxGroups\":2.5}}",
        "{\"name\":\"Regional Cup\",\"unknown\":true}",
        "{\"format\":{\"maxGroups\":2,\"unknown\":true}}"
    };

    private static object FullBody(string name, int maxGroups = 8, int maxTeamsPerGroup = 4) => new
    {
        name,
        format = new { type = "ROUND_ROBIN", maxGroups, maxTeamsPerGroup }
    };

    private async Task<TournamentDto> CreateTournament(string name, int maxGroups = 8, int maxTeamsPerGroup = 4)
    {
        var response = await Client.PostAsJsonAsync("/tournaments", FullBody(name, maxGroups, maxTeamsPerGroup));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read(response);
    }

    private async Task<TournamentDto> GetTournament(string id)
    {
        var response = await Client.GetAsync($"/tournaments/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Read(response);
    }

    private Task<HttpResponseMessage> SendJson(HttpMethod method, string path, string body) => Client.SendAsync(
        new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static async Task<TournamentDto> Read(HttpResponseMessage response)
    {
        var tournament = await response.Content.ReadFromJsonAsync<TournamentDto>();
        Assert.NotNull(tournament);
        return tournament;
    }

    private static async Task<List<TournamentDto>> ReadList(HttpResponseMessage response)
    {
        var tournaments = await response.Content.ReadFromJsonAsync<List<TournamentDto>>();
        Assert.NotNull(tournaments);
        return tournaments;
    }
}
