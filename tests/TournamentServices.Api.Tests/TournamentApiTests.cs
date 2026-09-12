using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests;

public abstract class TournamentApiTests : IDisposable
{
    protected readonly HttpClient Client;
    private readonly TournamentWebApplicationFactory _factory = new();

    protected TournamentApiTests()
    {
        Client = _factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}

public class TournamentWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITeamRepository>();
            services.AddSingleton<ITeamRepository, TeamRepository>();
        });
    }
}
