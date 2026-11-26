using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TournamentServices.Repositories;
using TournamentServices.Api.Monitoring;

namespace TournamentServices.Api.Tests;

public abstract class TournamentApiTests : IDisposable
{
    protected readonly HttpClient Client;
    private readonly TournamentWebApplicationFactory _factory = new();

    protected IServiceProvider Services => _factory.Services;

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
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITeamRepository>();
            services.AddScoped<ITeamRepository, EfTeamRepository>();
            services.RemoveAll<TournamentDbContext>();
            services.RemoveAll<DbContextOptions<TournamentDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TournamentDbContext>>();
            services.AddDbContext<TournamentDbContext>((provider, options) => options.UseSqlite(_connection)
                .AddInterceptors(provider.GetRequiredService<DatabaseTelemetryInterceptor>()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
