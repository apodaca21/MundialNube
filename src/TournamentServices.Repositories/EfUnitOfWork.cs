using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace TournamentServices.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(string? tournamentId = null, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>A mutation is committed only explicitly; disposal rolls back every saved change.</summary>
public sealed class EfUnitOfWork(TournamentDbContext db) : IUnitOfWork
{
    private IDbContextTransaction? _transaction;

    public async Task BeginAsync(string? tournamentId = null, CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A unit of work is already active.");
        _transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (tournamentId is not null && db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Tournaments\" WHERE \"Id\" = {tournamentId} FOR UPDATE", cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("Begin a unit of work before committing.");
        await db.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null) return;
        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
        db.ChangeTracker.Clear();
    }

    public async ValueTask DisposeAsync() => await RollbackAsync();
}
