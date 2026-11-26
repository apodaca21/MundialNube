using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Messaging;

public sealed class ChampionOutboxPublisher(IServiceScopeFactory scopes, IOptions<MessagingOptions> settings,
    ILogger<ChampionOutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Champion outbox delivery will be retried; events remain in the database."); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
    public async Task PublishBatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        var events = await db.OutboxEvents.AsNoTracking().Where(e => e.PublishedAtUtc == null)
            .OrderBy(e => e.OccurredAtUtc).Take(20).ToListAsync(ct);
        if (events.Count == 0) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await using var broker = new StompConnection();
        await broker.ConnectAsync(settings.Value, timeout.Token);
        foreach (var item in events)
        {
            var body = JsonSerializer.Serialize(new { eventId = item.Id, type = item.Type, payload = JsonSerializer.Deserialize<JsonElement>(item.Payload) });
            await broker.SendAsync("SEND", new()
            {
                ["destination"] = settings.Value.Destination,
                ["destination-type"] = "ANYCAST",
                ["persistent"] = "true",
                ["content-type"] = "application/json",
                ["receipt"] = item.Id
            }, body, timeout.Token);
            var receipt = await broker.ReadAsync(timeout.Token);
            if (receipt.Command != "RECEIPT" || receipt.Headers.GetValueOrDefault("receipt-id") != item.Id)
                throw new IOException("Broker did not confirm durable event delivery.");
            await db.OutboxEvents.Where(e => e.Id == item.Id && e.PublishedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.PublishedAtUtc, DateTime.UtcNow), ct);
        }
    }
}

public sealed class ChampionEventConsumer(IServiceScopeFactory scopes, IOptions<MessagingOptions> settings,
    ILogger<ChampionEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Short sessions reconnect while idle, avoiding stale sockets without a heartbeat writer.
                using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                session.CancelAfter(TimeSpan.FromSeconds(30));
                await using var broker = new StompConnection();
                await broker.ConnectAsync(settings.Value, session.Token);
                await broker.SendAsync("SUBSCRIBE", new()
                {
                    ["id"] = "champion-audit",
                    ["destination"] = settings.Value.Destination,
                    ["subscription-type"] = "ANYCAST",
                    ["ack"] = "client-individual",
                    ["consumer-window-size"] = "0"
                }, "", session.Token);
                while (!session.IsCancellationRequested)
                {
                    var frame = await broker.ReadAsync(session.Token);
                    if (frame.Command != "MESSAGE") continue;
                    if (!frame.Headers.TryGetValue("ack", out var ack)) throw new IOException("Missing STOMP acknowledgement id.");
                    try { await RecordAsync(frame.Body, session.Token); }
                    catch (JsonException)
                    {
                        logger.LogWarning("Invalid champion message rejected; broker redelivery/dead-letter policy applies.");
                        await broker.SendAsync("NACK", new() { ["id"] = ack }, "", session.Token);
                        continue;
                    }
                    // Acknowledge only after the idempotent database write commits.
                    await broker.SendAsync("ACK", new() { ["id"] = ack }, "", session.Token);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (OperationCanceledException) { }
            catch (Exception error) { logger.LogWarning(error, "Champion consumer reconnecting; unacknowledged messages will be redelivered."); }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
    public async Task<bool> RecordAsync(string message, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;
        // Valid JSON can still have the wrong shape; reject it through the NACK path.
        if (root.ValueKind != JsonValueKind.Object || !RequiredString(root, "eventId", out var id) ||
            !RequiredString(root, "type", out var eventType) || eventType != "champion.completed" ||
            !root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object ||
            !RequiredString(payload, "tournamentId", out _) || !RequiredString(payload, "teamId", out _))
            throw new JsonException("Invalid champion event envelope.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        // ON CONFLICT is supported by both deployed providers. ACK follows this committed statement.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ConsumedEvents" ("Id", "Type", "ConsumedAtUtc")
            VALUES ({id}, {"champion.completed"}, {DateTime.UtcNow})
            ON CONFLICT ("Id") DO NOTHING;
            """, ct);
        if (changed > 0) logger.LogInformation("Consumed champion event {EventId}", id);
        return changed > 0;
    }
    private static bool RequiredString(JsonElement parent, string name, out string value)
    {
        value = string.Empty;
        if (!parent.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String) return false;
        value = property.GetString()!;
        return !string.IsNullOrWhiteSpace(value);
    }
}
