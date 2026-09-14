using Moq;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class TournamentDelegateTests
{
    private readonly Mock<ITournamentRepository> _repo = new();
    private readonly TournamentDelegate _delegate;

    public TournamentDelegateTests()
    {
        _delegate = new TournamentDelegate(_repo.Object);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsTournamentsFromRepository()
    {
        var tournaments = new[] { NewTournament() };
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tournaments);

        var result = await _delegate.GetAllAsync();

        Assert.Same(tournaments, result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTournamentsExist()
    {
        _repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Tournament>());

        Assert.Empty(await _delegate.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTournament_WhenFound()
    {
        var tournament = SetupExistingTournament();

        Assert.Same(tournament, await _delegate.GetByIdAsync(tournament.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        Assert.Null(await _delegate.GetByIdAsync("missing"));
    }

    [Fact]
    public async Task CreateAsync_GeneratesId_AndSavesTournament()
    {
        var format = new TournamentFormat("ROUND_ROBIN", 8, 4);

        var result = await _delegate.CreateAsync("Mundial 2026", format);

        Assert.True(Guid.TryParse(result.Id, out _));
        Assert.Equal("Mundial 2026", result.Name);
        Assert.Equal(format, result.Format);
        _repo.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsEmptyName_WithoutSaving(string? name)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _delegate.CreateAsync(name!, new TournamentFormat("ROUND_ROBIN", 8, 4)));

        _repo.Verify(r => r.AddAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_RejectsNullFormat_WithoutSaving()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _delegate.CreateAsync("Mundial", null!));

        _repo.Verify(r => r.AddAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesNameAndFormat_WhenTournamentExists()
    {
        var tournament = SetupExistingTournament();
        var format = new TournamentFormat("ROUND_ROBIN", 12, 6);

        var result = await _delegate.UpdateAsync(tournament.Id, "Mundial actualizado", format);

        Assert.Equal(tournament.Id, result.Id);
        Assert.Equal("Mundial actualizado", result.Name);
        Assert.Equal(format, result.Format);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsNotFound_WithoutSaving()
    {
        await Assert.ThrowsAsync<TournamentNotFoundException>(() =>
            _delegate.UpdateAsync("missing", "Mundial", new TournamentFormat("ROUND_ROBIN", 8, 4)));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_RejectsEmptyName_WithoutChangingTournament(string? name)
    {
        var tournament = SetupExistingTournament();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _delegate.UpdateAsync(tournament.Id, name!, new TournamentFormat("ROUND_ROBIN", 12, 6)));

        AssertUnchanged(tournament);
    }

    [Fact]
    public async Task UpdateAsync_RejectsNullFormat_WithoutChangingTournament()
    {
        var tournament = SetupExistingTournament();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _delegate.UpdateAsync(tournament.Id, "Nombre nuevo", null!));

        AssertUnchanged(tournament);
    }

    [Fact]
    public async Task PatchAsync_ChangesOnlyName_WhenFormatIsOmitted()
    {
        var tournament = SetupExistingTournament();
        var originalFormat = tournament.Format;

        var result = await _delegate.PatchAsync(tournament.Id, "Nombre nuevo", null, null, null);

        Assert.Equal("Nombre nuevo", result.Name);
        Assert.Equal(originalFormat, result.Format);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_ChangesOnlyMaxGroups_WhenOtherFieldsAreOmitted()
    {
        var tournament = SetupExistingTournament();

        var result = await _delegate.PatchAsync(tournament.Id, null, null, 12, null);

        Assert.Equal("Mundial 2026", result.Name);
        Assert.Equal("ROUND_ROBIN", result.Format.Type);
        Assert.Equal(12, result.Format.MaxGroups);
        Assert.Equal(4, result.Format.MaxTeamsPerGroup);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_ChangesOnlyMaxTeamsPerGroup_WhenOtherFieldsAreOmitted()
    {
        var tournament = SetupExistingTournament();

        var result = await _delegate.PatchAsync(tournament.Id, null, null, null, 6);

        Assert.Equal("Mundial 2026", result.Name);
        Assert.Equal("ROUND_ROBIN", result.Format.Type);
        Assert.Equal(8, result.Format.MaxGroups);
        Assert.Equal(6, result.Format.MaxTeamsPerGroup);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_AcceptsTypeOnly_AndPreservesOtherFields()
    {
        var tournament = SetupExistingTournament();

        var result = await _delegate.PatchAsync(tournament.Id, null, "ROUND_ROBIN", null, null);

        Assert.Equal("Mundial 2026", result.Name);
        Assert.Equal(new TournamentFormat("ROUND_ROBIN", 8, 4), result.Format);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_ChangesAllSuppliedFields()
    {
        var tournament = SetupExistingTournament();

        var result = await _delegate.PatchAsync(tournament.Id, "Copa nueva", "ROUND_ROBIN", 12, 6);

        Assert.Equal(tournament.Id, result.Id);
        Assert.Equal("Copa nueva", result.Name);
        Assert.Equal(new TournamentFormat("ROUND_ROBIN", 12, 6), result.Format);
        _repo.Verify(r => r.UpdateAsync(result, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PatchAsync_PreservesAllFields_WhenAllValuesAreNull()
    {
        var tournament = SetupExistingTournament();

        var result = await _delegate.PatchAsync(tournament.Id, null, null, null, null);

        Assert.Equal(tournament.Id, result.Id);
        Assert.Equal("Mundial 2026", result.Name);
        Assert.Equal(new TournamentFormat("ROUND_ROBIN", 8, 4), result.Format);
    }

    [Fact]
    public async Task PatchAsync_ThrowsNotFound_WithoutSaving()
    {
        await Assert.ThrowsAsync<TournamentNotFoundException>(() =>
            _delegate.PatchAsync("missing", "Copa", null, null, null));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PatchAsync_RejectsEmptyName_WithoutChangingTournament(string name)
    {
        var tournament = SetupExistingTournament();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _delegate.PatchAsync(tournament.Id, name, null, 12, 6));

        AssertUnchanged(tournament);
    }

    [Theory]
    [InlineData("KNOCKOUT", null, null)]
    [InlineData("", null, null)]
    [InlineData(null, 0, null)]
    [InlineData(null, 17, null)]
    [InlineData(null, null, 1)]
    [InlineData(null, null, 9)]
    public async Task PatchAsync_RejectsInvalidFormat_WithoutChangingNameOrFormat(
        string? formatType, int? maxGroups, int? maxTeamsPerGroup)
    {
        var tournament = SetupExistingTournament();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _delegate.PatchAsync(tournament.Id, "Nombre nuevo", formatType, maxGroups, maxTeamsPerGroup));

        AssertUnchanged(tournament);
    }

    [Fact]
    public async Task DeleteAsync_UsesRepositoryDelete_WhenTournamentExists()
    {
        _repo.Setup(r => r.DeleteAsync("tournament-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _delegate.DeleteAsync("tournament-1");

        _repo.Verify(r => r.DeleteAsync("tournament-1", It.IsAny<CancellationToken>()), Times.Once);
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotFound_WhenRepositoryReturnsFalse()
    {
        _repo.Setup(r => r.DeleteAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<TournamentNotFoundException>(() => _delegate.DeleteAsync("missing"));

        _repo.Verify(r => r.DeleteAsync("missing", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Operations_PassCancellationTokenToRepository()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var tournament = NewTournament();
        _repo.Setup(r => r.GetAllAsync(token)).ReturnsAsync(new[] { tournament });
        _repo.Setup(r => r.GetByIdAsync(tournament.Id, token)).ReturnsAsync(tournament);
        _repo.Setup(r => r.DeleteAsync(tournament.Id, token)).ReturnsAsync(true);

        await _delegate.GetAllAsync(token);
        await _delegate.GetByIdAsync(tournament.Id, token);
        await _delegate.CreateAsync("Otra copa", tournament.Format, token);
        await _delegate.UpdateAsync(tournament.Id, "Copa actualizada", tournament.Format, token);
        await _delegate.PatchAsync(tournament.Id, "Copa final", null, null, null, token);
        await _delegate.DeleteAsync(tournament.Id, token);

        _repo.Verify(r => r.GetAllAsync(token), Times.Once);
        _repo.Verify(r => r.GetByIdAsync(tournament.Id, token), Times.Exactly(3));
        _repo.Verify(r => r.AddAsync(It.IsAny<Tournament>(), token), Times.Once);
        _repo.Verify(r => r.UpdateAsync(tournament, token), Times.Exactly(2));
        _repo.Verify(r => r.DeleteAsync(tournament.Id, token), Times.Once);
        _repo.VerifyNoOtherCalls();
    }

    private Tournament SetupExistingTournament()
    {
        var tournament = NewTournament();
        _repo.Setup(r => r.GetByIdAsync(tournament.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        return tournament;
    }

    private static Tournament NewTournament() => new()
    {
        Id = "tournament-1",
        Name = "Mundial 2026",
        Format = new TournamentFormat("ROUND_ROBIN", 8, 4)
    };

    private void AssertUnchanged(Tournament tournament)
    {
        Assert.Equal("Mundial 2026", tournament.Name);
        Assert.Equal(new TournamentFormat("ROUND_ROBIN", 8, 4), tournament.Format);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
