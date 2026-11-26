using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TournamentServices.Repositories.Migrations;

/// <summary>Uses separate generated snapshots while sharing explicitly provider-aware migrations.</summary>
public sealed class ProviderMigrationsAssembly(ICurrentDbContext currentContext) : IMigrationsAssembly
{
    public Assembly Assembly => typeof(TournamentDbContext).Assembly;

    public IReadOnlyDictionary<string, TypeInfo> Migrations => Assembly.DefinedTypes
        .Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(Migration)) &&
            type.GetCustomAttribute<DbContextAttribute>()?.ContextType == typeof(TournamentDbContext))
        .Select(type => (Type: type, Id: type.GetCustomAttribute<MigrationAttribute>()?.Id))
        .Where(pair => pair.Id is not null).OrderBy(pair => pair.Id)
        .ToDictionary(pair => pair.Id!, pair => pair.Type);

    public ModelSnapshot ModelSnapshot => currentContext.Context.Database.ProviderName?.Contains("Npgsql") == true
        ? new PostgresTournamentDbContextModelSnapshot()
        : new SqliteTournamentDbContextModelSnapshot();

    public string? FindMigrationId(string nameOrId) => Migrations.Keys.FirstOrDefault(id =>
        string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id[(id.IndexOf('_') + 1)..], nameOrId, StringComparison.OrdinalIgnoreCase));

    public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
    {
        var migration = (Migration)Activator.CreateInstance(migrationClass.AsType())!;
        migration.ActiveProvider = activeProvider;
        return migration;
    }
}
