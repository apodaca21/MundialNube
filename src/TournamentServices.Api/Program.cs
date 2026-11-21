using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using TournamentServices.Api.Dtos;
using TournamentServices.Api.Monitoring;
using TournamentServices.Api.Routes;
using TournamentServices.Api.Validators;
using TournamentServices.Delegates;
using TournamentServices.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton(Metrics.NewCustomRegistry());
builder.Services.AddSingleton<TournamentMetrics>();

builder.Services.AddSingleton<TeamRepository>();
builder.Services.AddScoped<ITeamRepository, TournamentTeamCatalogRepository>();
builder.Services.AddSingleton<IGroupRepository, GroupRepository>();
builder.Services.AddSingleton<IMatchRepository, MatchRepository>();
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

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TournamentsConnection")));
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<ITournamentResultsRepository, TournamentResultsRepository>();
builder.Services.AddScoped<ITournamentDelegate, TournamentDelegate>();
builder.Services.AddScoped<IValidator<CreateTournamentDto>, CreateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTournamentDto>, UpdateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<PatchTournamentDto>, PatchTournamentDtoValidator>();

var app = builder.Build();

// Conserva las bases existentes y añade el historial y los equipos de cada torneo.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
    await TournamentDatabaseInitializer.InitializeAsync(database);
}

app.UseRouting();
app.UseHttpMetrics(options =>
    options.SetMetricFactory(Metrics.WithCustomRegistry(app.Services.GetRequiredService<CollectorRegistry>())));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => TypedResults.Ok("Services running"));
app.MapGet("/metrics", (HttpContext context, TournamentMetrics metrics) => metrics.WriteAsync(context))
    .ExcludeFromDescription();
app.MapTeamRoutes();
app.MapTournamentRoutes();
app.MapTournamentResultRoutes();
app.MapGroupRoutes();
app.MapMatchRoutes();

app.Run();

public partial class Program;
