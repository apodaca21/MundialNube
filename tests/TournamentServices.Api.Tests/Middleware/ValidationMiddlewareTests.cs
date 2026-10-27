using System.Net;
using System.Net.Http.Json;
using System.Text;
using TournamentServices.Api.Dtos;

namespace TournamentServices.Api.Tests.Middleware;

public class ValidationMiddlewareTests : TournamentApiTests
{
    [Fact]
    public async Task Post_Team_Returns400_WithProblemDetails_WhenNameIsEmpty()
    {
        var response = await Client.PostAsJsonAsync("/teams", new CreateTeamDto(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_Tournament_Returns400_WhenJsonHasUnknownFields()
    {
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/tournaments")
        {
            Content = new StringContent(
                "{\"name\":\"World Cup\",\"format\":{\"type\":\"FIFA\",\"maxGroups\":8,\"maxTeamsPerGroup\":4},\"extra\":true}",
                Encoding.UTF8,
                "application/json")
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Services running", await response.Content.ReadFromJsonAsync<string>());
    }
}
