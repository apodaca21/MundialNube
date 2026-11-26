using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using TournamentServices.Api;
using TournamentServices.Api.Messaging;
using TournamentServices.Api.Dtos;
using TournamentServices.Api.Monitoring;
using TournamentServices.Api.Routes;
using TournamentServices.Api.Validators;
using TournamentServices.Delegates;
using TournamentServices.Repositories;

if (args.Contains("--consume"))
{
    var worker = Host.CreateApplicationBuilder(args.Where(a => a != "--consume").ToArray());
    worker.Services.AddTournamentDatabase(worker.Configuration);
    worker.Services.AddHostedService<ChampionEventConsumer>();
    await worker.Build().RunAsync();
    return;
}

var migrationOnly = args.Contains("--migrate");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--migrate").ToArray());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTournamentDatabase(builder.Configuration);
builder.Services.AddSingleton<TournamentMetrics>();

builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<ITeamRepository, EfTeamRepository>();
builder.Services.AddScoped<IGroupRepository, EfGroupRepository>();
builder.Services.AddScoped<IMatchRepository, EfMatchRepository>();
builder.Services.AddScoped<IWorldCupSimulation, WorldCupSimulation>();
builder.Services.AddSingleton<IScoreGenerator, RandomScoreGenerator>();
if (builder.Configuration.GetValue<bool>("Messaging:Enabled") && !migrationOnly)
    builder.Services.AddHostedService<ChampionOutboxPublisher>();
builder.Services.AddScoped<ITeamDelegate, TeamDelegate>();
builder.Services.AddScoped<IGroupDelegate, GroupDelegate>();
builder.Services.AddScoped<IMatchDelegate, MatchDelegate>();
builder.Services.AddScoped<IValidator<CreateTeamDto>, CreateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTeamDto>, UpdateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<CreateGroupDto>, CreateGroupDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateGroupDto>, UpdateGroupDtoValidator>();
builder.Services.AddScoped<IValidator<AssignTeamsDto>, AssignTeamsDtoValidator>();
builder.Services.AddScoped<IValidator<CreateMatchDto>, CreateMatchDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateScoreDto>, UpdateScoreDtoValidator>();


builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<ITournamentResultsRepository, TournamentResultsRepository>();
builder.Services.AddScoped<ITournamentDelegate, TournamentDelegate>();
builder.Services.AddScoped<IValidator<CreateTournamentDto>, CreateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTournamentDto>, UpdateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<PatchTournamentDto>, PatchTournamentDtoValidator>();

var app = builder.Build();

// PostgreSQL replicas only serve requests. Deployments run --migrate as a separate Job.
if (migrationOnly || (builder.Configuration.GetValue<bool?>("Database:MigrateOnStartup")
    ?? !ServiceConfiguration.IsPostgres(builder.Configuration)))
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
    await TournamentDatabaseInitializer.InitializeAsync(database);
}

if (migrationOnly) return;

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Pod-Name"] = Environment.GetEnvironmentVariable("POD_NAME") ?? Environment.MachineName;
    await next(context);
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseHttpMetrics(options =>
    options.SetMetricFactory(Metrics.WithCustomRegistry(app.Services.GetRequiredService<CollectorRegistry>())));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => TypedResults.Ok("Services running"));
app.MapGet("/ready", async (TournamentDbContext db, CancellationToken ct) =>
{
    try { await db.Tournaments.AnyAsync(ct); return Results.Ok(new { status = "ready" }); }
    catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
});
app.MapGet("/metrics", (HttpContext context, TournamentMetrics metrics) => metrics.WriteAsync(context))
    .ExcludeFromDescription();
app.MapTeamRoutes();
app.MapTournamentRoutes();
app.MapTournamentResultRoutes();
app.MapGroupRoutes();
app.MapMatchRoutes();
app.MapWorldCupRoutes();

app.Run();

public partial class Program;
