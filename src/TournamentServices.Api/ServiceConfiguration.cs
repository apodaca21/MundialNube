using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using TournamentServices.Api.Messaging;
using TournamentServices.Api.Monitoring;
using TournamentServices.Repositories;

namespace TournamentServices.Api;

public static class ServiceConfiguration
{
    public static IServiceCollection AddTournamentDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(Metrics.NewCustomRegistry());
        services.AddSingleton<DatabaseTelemetryInterceptor>();
        services.AddDbContext<TournamentDbContext>((provider, options) =>
        {
            var connection = configuration.GetConnectionString("TournamentsConnection") ?? "Data Source=tournaments.db;Foreign Keys=True";
            if (IsPostgres(configuration)) options.UseNpgsql(connection);
            else options.UseSqlite(connection);
            options.AddInterceptors(provider.GetRequiredService<DatabaseTelemetryInterceptor>());
        });
        services.Configure<MessagingOptions>(configuration.GetSection("Messaging"));
        services.AddOpenTelemetry().ConfigureResource(r => r.AddService("mundialnube-api"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(o => o.Filter = context => context.Request.Path != "/metrics" && context.Request.Path != "/health")
                    .AddSource(DatabaseTelemetryInterceptor.ActivitySourceName);
                if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) tracing.AddOtlpExporter();
            });
        return services;
    }
    public static bool IsPostgres(IConfiguration configuration) =>
        string.Equals(configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(configuration["Database:Provider"], "PostgreSQL", StringComparison.OrdinalIgnoreCase);
}
