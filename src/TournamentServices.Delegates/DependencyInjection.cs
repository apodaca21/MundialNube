using Microsoft.Extensions.DependencyInjection;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates;

public static class DependencyInjection
{
    public static IServiceCollection AddTeamServices(this IServiceCollection services)
    {
        services.AddSingleton<ITeamRepository, TeamRepository>();
        services.AddScoped<ITeamDelegate, TeamDelegate>();
        return services;
    }
}
