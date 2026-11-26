using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TournamentServices.Repositories;

/// <summary>PostgreSQL is the production migration model; SQLite is kept for local compatibility tests.</summary>
public sealed class TournamentDbContextFactory : IDesignTimeDbContextFactory<TournamentDbContext>
{
    public TournamentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TournamentDbContext>();
        if (Environment.GetEnvironmentVariable("TOURNAMENT_MIGRATION_PROVIDER") == "Sqlite")
            options.UseSqlite("Data Source=migrations-design.db");
        else options.UseNpgsql(Environment.GetEnvironmentVariable("TOURNAMENT_MIGRATION_CONNECTION")
            ?? "Host=localhost;Database=tournaments;Username=tournaments");
        return new TournamentDbContext(options.Options);
    }
}
