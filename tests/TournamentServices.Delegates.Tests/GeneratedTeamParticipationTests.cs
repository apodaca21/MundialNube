using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public sealed class GeneratedTeamParticipationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:;Foreign Keys=True");
    private TournamentDbContext _db = null!;
    private TournamentRepository _tournaments = null!;
    private readonly TeamRepository _manualTeams = new();
    private TournamentTeamCatalogRepository _teams = null!;
    private readonly GroupRepository _groups = new();
    private readonly MatchRepository _matches = new();
    private GroupDelegate Groups => new(_groups, _matches, _teams, _tournaments);
    private MatchDelegate Matches => new(_matches, _groups, _teams, _tournaments);

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new TournamentDbContext(new DbContextOptionsBuilder<TournamentDbContext>().UseSqlite(_connection).Options);
        await TournamentDatabaseInitializer.InitializeAsync(_db);
        _tournaments = new TournamentRepository(_db);
        _teams = new TournamentTeamCatalogRepository(_db, _manualTeams);
        await _tournaments.AddAsync(new Tournament { Id = "one", Name = "One" });
        await _tournaments.AddAsync(new Tournament { Id = "two", Name = "Two" });
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GroupAssignment_RejectsGeneratedTeamsFromAnotherTournamentWithoutChangingGroup()
    {
        var own = (await new TournamentResultsRepository(_db).GetTeamsAsync("one"))!;
        var foreign = (await new TournamentResultsRepository(_db).GetTeamsAsync("two"))!;
        var groupId = await Groups.CreateAsync("one", "A");
        await Groups.AssignTeamsAsync("one", groupId, [own[0].Id, own[1].Id]);

        await Assert.ThrowsAsync<GroupRuleViolationException>(() =>
            Groups.AssignTeamsAsync("one", groupId, [own[0].Id, foreign[0].Id]));

        Assert.Equal(new[] { own[0].Id, own[1].Id }, (await Groups.GetByIdAsync("one", groupId))!.TeamIds);
    }

    [Fact]
    public async Task MatchCreation_RejectsForeignGeneratedTeamsEvenWithoutGroup()
    {
        var own = (await new TournamentResultsRepository(_db).GetTeamsAsync("one"))!;
        var foreign = (await new TournamentResultsRepository(_db).GetTeamsAsync("two"))!;
        await Assert.ThrowsAsync<MatchRuleViolationException>(() =>
            Matches.CreateAsync("one", null, own[0].Id, foreign[0].Id));
        Assert.Empty(await Matches.GetByTournamentAsync("one"));

        var created = await Matches.CreateAsync("one", null, own[0].Id, own[1].Id);
        Assert.NotNull(await Matches.GetByIdAsync("one", created));
    }

    [Fact]
    public async Task ManualGlobalTeams_RemainUsableInMultipleTournaments()
    {
        await _teams.AddAsync(new Team { Id = "manual-a", Name = "Manual A" });
        await _teams.AddAsync(new Team { Id = "manual-b", Name = "Manual B" });
        foreach (var tournamentId in new[] { "one", "two" })
        {
            var groupId = await Groups.CreateAsync(tournamentId, "A");
            await Groups.AssignTeamsAsync(tournamentId, groupId, ["manual-a", "manual-b"]);
            var matchId = await Matches.CreateAsync(tournamentId, groupId, "manual-a", "manual-b");
            Assert.NotNull(await Matches.GetByIdAsync(tournamentId, matchId));
        }
    }

    [Fact]
    public async Task GeneratedTeamOwnership_SurvivesRenameAndAllCatalogLookups()
    {
        var original = (await new TournamentResultsRepository(_db).GetTeamsAsync("one"))![0];
        await new TeamDelegate(_teams).UpdateAsync(original.Id, "Renamed generated team");
        Assert.Equal("one", (await _teams.GetByIdAsync(original.Id))!.TournamentId);
        Assert.Equal("one", (await _teams.GetByNameAsync("Renamed generated team"))!.TournamentId);
        Assert.Equal("one", (await _teams.GetAllAsync()).Single(team => team.Id == original.Id).TournamentId);
    }
}
