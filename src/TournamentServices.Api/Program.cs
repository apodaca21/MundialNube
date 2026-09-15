using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Api.Dtos;
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

builder.Services.AddSingleton<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<ITeamDelegate, TeamDelegate>();
builder.Services.AddScoped<IValidator<CreateTeamDto>, CreateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTeamDto>, UpdateTeamDtoValidator>();

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TournamentsConnection")));
builder.Services.AddScoped<ITournamentRepository, TournamentRepository>();
builder.Services.AddScoped<ITournamentDelegate, TournamentDelegate>();
builder.Services.AddScoped<IValidator<CreateTournamentDto>, CreateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTournamentDto>, UpdateTournamentDtoValidator>();
builder.Services.AddScoped<IValidator<PatchTournamentDto>, PatchTournamentDtoValidator>();

var app = builder.Build();

// Para la demostración, crea el archivo y las tablas en el primer arranque.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
    await database.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => TypedResults.Ok("Services running"));
app.MapTeamRoutes();
app.MapTournamentRoutes();

app.Run();

public partial class Program;
