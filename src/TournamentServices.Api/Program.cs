using FluentValidation;
using TournamentServices.Api.Dtos;
using TournamentServices.Api.Validators;
using TournamentServices.Delegates;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTeamServices();
builder.Services.AddScoped<IValidator<CreateTeamDto>, CreateTeamDtoValidator>();
builder.Services.AddScoped<IValidator<UpdateTeamDto>, UpdateTeamDtoValidator>();

var app = builder.Build();
app.Run();
