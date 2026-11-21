using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Monitoring;

/// <summary>Exports current database facts, including after an application restart.</summary>
public sealed class TournamentMetrics : IDisposable
{
    private readonly CollectorRegistry _registry;
    private readonly SemaphoreSlim _scrapeLock = new(1, 1);
    private readonly Gauge _tournaments;
    private readonly Gauge _champions;
    private readonly Gauge _latest;
    private readonly Gauge _recent;

    public TournamentMetrics(CollectorRegistry registry)
    {
        _registry = registry;
        var factory = Metrics.WithCustomRegistry(registry);
        _tournaments = factory.CreateGauge("mundial_tournaments_total", "Tournaments currently stored in SQLite.");
        _champions = factory.CreateGauge("mundial_champions_total", "Champion records stored in SQLite.");
        _latest = factory.CreateGauge("mundial_latest_champion_info", "Most recently recorded champion.",
            ["team_name", "tournament_name"]);
        _recent = factory.CreateGauge("mundial_recent_champion_info", "Latest 20 champions, rank 1 is newest.",
            ["rank", "team_name", "tournament_name", "completed_at"]);
    }

    public async Task WriteAsync(HttpContext context)
    {
        var ct = context.RequestAborted;
        // Serialize the refresh AND export so overlapping scrapes never mix snapshots.
        await _scrapeLock.WaitAsync(ct);
        try
        {
            var db = context.RequestServices.GetRequiredService<TournamentDbContext>();
            var results = context.RequestServices.GetRequiredService<ITournamentResultsRepository>();
            var tournamentCount = await db.Tournaments.CountAsync(ct);
            var championCount = await db.Set<TournamentServices.Domain.TournamentChampion>().CountAsync(ct);
            var champions = await results.GetRecentChampionsAsync(20, ct);

            _tournaments.Set(tournamentCount);
            _champions.Set(championCount);
            // Remove old labels: only one latest champion and at most twenty history series.
            foreach (var labels in _latest.GetAllLabelValues())
                _latest.WithLabels(labels).Remove();
            foreach (var labels in _recent.GetAllLabelValues())
                _recent.WithLabels(labels).Remove();
            if (champions.Count > 0)
                _latest.WithLabels(champions[0].TeamName, champions[0].TournamentName).Set(1);
            for (var i = 0; i < champions.Count; i++)
            {
                var champion = champions[i];
                _recent.WithLabels((i + 1).ToString(CultureInfo.InvariantCulture), champion.TeamName,
                    champion.TournamentName, champion.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Set(1);
            }

            // A failed database read fails the scrape instead of publishing stale business data.
            context.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
            await _registry.CollectAndExportAsTextAsync(context.Response.Body, ct);
        }
        finally
        {
            _scrapeLock.Release();
        }
    }

    public void Dispose() => _scrapeLock.Dispose();
}
