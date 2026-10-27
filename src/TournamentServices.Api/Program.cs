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

builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<ITeamDelegate, TeamDelegate>();
builder.Services.AddScoped<IGroupDelegate, GroupDelegate>();
builder.Services.AddScoped<IMatchDelegate, MatchDelegate>();
builder.Services.AddScoped<IMundialDelegate, MundialDelegate>();
builder.Services.AddScoped<IValidator<CreateTeamDto>, CreateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTeamDto>, UpdateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<CreateGroupDto>, CreateGroupDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateGroupDto>, UpdateGroupDtoValidator>();
builder.Services.AddScoped<IValidator<AssignTeamsDto>, AssignTeamsDtoValidator>();
builder.Services.AddScoped<IValidator<CreateMatchDto>, CreateMatchDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateScoreDto>, UpdateScoreDtoValidator>();

builder.Services.AddDbContext<TournamentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
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
    if (database.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
    {
        await database.Database.ExecuteSqlRawAsync(
            """ALTER TABLE "Matches" ADD COLUMN IF NOT EXISTS "Stage" text NOT NULL DEFAULT 'GROUP';""");
        await database.Database.ExecuteSqlRawAsync(
            """UPDATE "Tournaments" SET "Format_Type" = 'FIFA' WHERE "Format_Type" IS DISTINCT FROM 'FIFA';""");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => TypedResults.Ok("Services running"));
app.MapTeamRoutes();
app.MapTournamentRoutes();
app.MapGroupRoutes();
app.MapMatchRoutes();
app.MapMundialRoutes();

app.Run();

public partial class Program;
