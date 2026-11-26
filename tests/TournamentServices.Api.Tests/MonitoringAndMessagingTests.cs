using System.Net;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prometheus;
using TournamentServices.Api.Monitoring;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TournamentServices.Api.Messaging;
using TournamentServices.Repositories;

namespace TournamentServices.Api.Tests;

public class MonitoringAndMessagingTests : TournamentApiTests
{
    [Fact]
    public async Task ActualQueriesAndHttpRequestsExposeMetricsAndPodIdentity()
    {
        var response = await Client.GetAsync("/tournaments");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Pod-Name"));
        var metrics = await Client.GetStringAsync("/metrics");
        Assert.Contains("mundial_database_command_duration_seconds_count{provider=\"sqlite\",operation=\"SELECT\",outcome=\"success\"}", metrics);
        Assert.Contains("http_request_duration_seconds_count", metrics);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/ready")).StatusCode);
    }

    [Fact]
    public async Task ConsumerRecordsEachEventOnceAndRejectsMalformedPayloads()
    {
        var consumer = new ChampionEventConsumer(Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MessagingOptions()), NullLogger<ChampionEventConsumer>.Instance);
        var message = JsonSerializer.Serialize(new { eventId = "event-one", type = "champion.completed", payload = new { tournamentId = "cup", teamId = "team" } });
        Assert.True(await consumer.RecordAsync(message, default));
        Assert.False(await consumer.RecordAsync(message, default));
        await Assert.ThrowsAsync<JsonException>(() => consumer.RecordAsync("{}", default));
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TournamentDbContext>();
        Assert.Equal(1, await db.ConsumedEvents.CountAsync());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"eventId\":\"x\",\"type\":123,\"payload\":{}}")]
    [InlineData("{\"eventId\":\"x\",\"type\":\"champion.completed\",\"payload\":[]}")]
    [InlineData("{\"eventId\":\"x\",\"type\":\"champion.completed\",\"payload\":{\"tournamentId\":null,\"teamId\":\"team\"}}")]
    [InlineData("{\"eventId\":\"x\",\"type\":\"champion.completed\",\"payload\":{\"tournamentId\":\"cup\",\"teamId\":123}}")]
    public async Task ConsumerRejectsWrongJsonShapesThroughTheNackExceptionPath(string message)
    {
        var consumer = new ChampionEventConsumer(Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MessagingOptions()), NullLogger<ChampionEventConsumer>.Instance);
        await Assert.ThrowsAsync<JsonException>(() => consumer.RecordAsync(message, default));
        using var scope = Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<TournamentDbContext>().ConsumedEvents.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledDatabaseCommandsCloseTheSpanAndPublishACancellationMetric(bool asynchronous)
    {
        var registry = Metrics.NewCustomRegistry();
        var interceptor = new DatabaseTelemetryInterceptor(registry);
        Activity? started = null;
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DatabaseTelemetryInterceptor.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => started = activity,
            ActivityStopped = activity => stopped = activity
        };
        ActivitySource.AddActivityListener(listener);
        using var connection = new SqliteConnection("Data Source=:memory:");
        using var command = connection.CreateCommand(); command.CommandText = "SELECT 1";
        var commandId = Guid.NewGuid(); var connectionId = Guid.NewGuid(); var start = DateTimeOffset.UtcNow;
        // Exercise EF's cancellation callback contract without a timing-sensitive live SQL timeout.
        var beginning = new CommandEventData(null!, (_, _) => "", connection, command, command.CommandText, null,
            DbCommandMethod.ExecuteScalar, commandId, connectionId, asynchronous, false, start, CommandSource.Unknown);
        var ending = new CommandEndEventData(null!, (_, _) => "", connection, command, command.CommandText, null,
            DbCommandMethod.ExecuteScalar, commandId, connectionId, asynchronous, false, start, TimeSpan.FromMilliseconds(12), CommandSource.Unknown);
        if (asynchronous)
        {
            await interceptor.ScalarExecutingAsync(command, beginning, default);
            await interceptor.CommandCanceledAsync(command, ending);
        }
        else
        {
            interceptor.ScalarExecuting(command, beginning, default);
            interceptor.CommandCanceled(command, ending);
        }
        Assert.NotNull(started); Assert.NotNull(stopped); Assert.Same(started, stopped);
        Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        using var output = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(output);
        Assert.Contains("mundial_database_command_duration_seconds_count{provider=\"sqlite\",operation=\"SELECT\",outcome=\"canceled\"} 1",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task StompTransportUsesUtf8ByteLengthAndDecodesEscapedHeaders()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
            var stream = peer.GetStream();
            async Task<string> ReadFrame()
            {
                using var buffer = new MemoryStream(); var one = new byte[1];
                while (true) { await stream.ReadExactlyAsync(one, timeout.Token); if (one[0] == 0) break; buffer.WriteByte(one[0]); }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
            Assert.StartsWith("CONNECT\n", await ReadFrame());
            await stream.WriteAsync(Encoding.UTF8.GetBytes("CONNECTED\nversion:1.2\n\n\0"), timeout.Token);
            var sent = await ReadFrame();
            Assert.Contains("content-length:7\n", sent); // 'México' is seven UTF-8 bytes.
            Assert.Contains("x-label:a\\cb\\nc\n", sent);
            await stream.WriteAsync(Encoding.UTF8.GetBytes("RECEIPT\nreceipt-id:a\\cb\ncontent-length:0\n\n\0"), timeout.Token);
        });
        await using (var broker = new StompConnection())
        {
            await broker.ConnectAsync(new MessagingOptions { Port = port, Host = "127.0.0.1" }, timeout.Token);
            await broker.SendAsync("SEND", new() { ["destination"] = "/queue/test", ["x-label"] = "a:b\nc" }, "México", timeout.Token);
            var receipt = await broker.ReadAsync(timeout.Token);
            Assert.Equal("a:b", receipt.Headers["receipt-id"]);
        }
        await server;
    }
}
