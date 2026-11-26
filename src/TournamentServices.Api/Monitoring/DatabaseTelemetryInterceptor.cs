using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prometheus;

namespace TournamentServices.Api.Monitoring;

public sealed class DatabaseTelemetryInterceptor : DbCommandInterceptor
{
    public const string ActivitySourceName = "MundialNube.Database";
    public static readonly ActivitySource Activities = new(ActivitySourceName);
    private readonly Histogram _duration;
    private readonly ConcurrentDictionary<Guid, Activity> _activities = new();
    public DatabaseTelemetryInterceptor(CollectorRegistry registry)
    {
        _duration = Metrics.WithCustomRegistry(registry).CreateHistogram(
            "mundial_database_command_duration_seconds", "Actual EF database command duration observed by the API, including network time.",
            new HistogramConfiguration
            {
                LabelNames = ["provider", "operation", "outcome"],
                Buckets = Histogram.ExponentialBuckets(.0001, 2, 18)
            });
    }
    private static string Operation(DbCommand command)
    {
        var word = command.CommandText.TrimStart().Split([' ', '\r', '\n', '\t'], 2)[0].ToUpperInvariant();
        return word is "SELECT" or "INSERT" or "UPDATE" or "DELETE" ? word : "OTHER";
    }
    private static string Provider(DbCommand command) => command.GetType().Name.Contains("Npgsql") ? "postgresql" : "sqlite";
    private void Start(DbCommand command, CommandEventData data)
    {
        var activity = Activities.StartActivity("database." + Operation(command).ToLowerInvariant(), ActivityKind.Client);
        if (activity is null) return;
        activity.SetTag("db.system.name", Provider(command));
        activity.SetTag("db.operation.name", Operation(command));
        _activities[data.CommandId] = activity;
    }
    private void Finish(DbCommand command, CommandEndEventData data, string outcome = "success")
    {
        _duration.WithLabels(Provider(command), Operation(command), outcome).Observe(data.Duration.TotalSeconds);
        if (_activities.TryRemove(data.CommandId, out var activity))
        {
            if (outcome != "success") activity.SetStatus(ActivityStatusCode.Error, outcome);
            activity.Dispose();
        }
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Start(c, e); return r; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Start(c, e); return ValueTask.FromResult(r); }
    public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { Finish(c, e); return r; }
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken ct = default) { Finish(c, e); return ValueTask.FromResult(r); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Start(c, e); return r; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Start(c, e); return ValueTask.FromResult(r); }
    public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Finish(c, e); return r; }
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken ct = default) { Finish(c, e); return ValueTask.FromResult(r); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand c, CommandEventData e, InterceptionResult<object> r) { Start(c, e); return r; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Start(c, e); return ValueTask.FromResult(r); }
    public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Finish(c, e); return r; }
    public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken ct = default) { Finish(c, e); return ValueTask.FromResult(r); }
    public override void CommandCanceled(DbCommand c, CommandEndEventData e) => Finish(c, e, "canceled");
    public override Task CommandCanceledAsync(DbCommand c, CommandEndEventData e, CancellationToken ct = default) { Finish(c, e, "canceled"); return Task.CompletedTask; }
    public override void CommandFailed(DbCommand c, CommandErrorEventData e) => Finish(c, e, "error");
    public override Task CommandFailedAsync(DbCommand c, CommandErrorEventData e, CancellationToken ct = default) { Finish(c, e, "error"); return Task.CompletedTask; }
}
