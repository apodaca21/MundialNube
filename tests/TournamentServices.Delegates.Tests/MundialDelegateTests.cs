using Moq;
using TournamentServices.Domain;
using TournamentServices.Domain.Exceptions;
using TournamentServices.Repositories;

namespace TournamentServices.Delegates.Tests;

public class MundialDelegateTests
{
    [Fact]
    public async Task SimulateAsync_Throws_WhenTournamentIsMissing()
    {
        var tournaments = new Mock<ITournamentRepository>();
        tournaments.Setup(repo => repo.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);

        var mundial = new MundialDelegate(
            tournaments.Object,
            new Mock<ITeamRepository>().Object,
            new Mock<IGroupRepository>().Object,
            new Mock<IMatchRepository>().Object);

        await Assert.ThrowsAsync<TournamentNotFoundException>(() => mundial.SimulateAsync("missing"));
    }
}
