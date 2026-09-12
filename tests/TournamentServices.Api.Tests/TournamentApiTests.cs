using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests;

public abstract class TournamentApiTests : IDisposable
{
    protected TournamentWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected TournamentApiTests()
    {
        Factory = new TournamentWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class TournamentWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITeamRepository>();
            services.AddSingleton<ITeamRepository, TeamRepository>();
        });
    }
}
