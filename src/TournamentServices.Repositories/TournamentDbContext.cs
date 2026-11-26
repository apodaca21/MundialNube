using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using TournamentServices.Repositories.Migrations;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TournamentDbContext(DbContextOptions<TournamentDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentGroup> Groups => Set<TournamentGroup>();
    public DbSet<TournamentMatch> Matches => Set<TournamentMatch>();

    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TournamentChampion> TournamentChampions => Set<TournamentChampion>();
    public DbSet<Team> ManualTeams => Set<Team>();
    public DbSet<GroupTeam> GroupTeams => Set<GroupTeam>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ConsumedEvent> ConsumedEvents => Set<ConsumedEvent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ReplaceService<IMigrationsAssembly, ProviderMigrationsAssembly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>().ToTable("ManualTeams");
        modelBuilder.Entity<Team>().Ignore(team => team.TournamentId);
        modelBuilder.Entity<Team>().HasIndex(team => team.Name).IsUnique();
        modelBuilder.Entity<GroupTeam>().HasKey(team => new { team.GroupId, team.TeamId });
        modelBuilder.Entity<GroupTeam>().HasOne<TournamentGroup>().WithMany()
            .HasForeignKey(team => team.GroupId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GroupTeam>().HasIndex(team => team.TeamId);
        modelBuilder.Entity<OutboxEvent>().HasIndex(message => new { message.PublishedAtUtc, message.OccurredAtUtc });
        modelBuilder.Entity<Tournament>().OwnsOne(tournament => tournament.Format, format =>
        {
            format.Property(value => value.Type).IsRequired();
            format.Property(value => value.MaxGroups);
            format.Property(value => value.MaxTeamsPerGroup);
        });
        modelBuilder.Entity<Tournament>().Navigation(tournament => tournament.Format).IsRequired();

        modelBuilder.Entity<TournamentGroup>()
            .HasOne<Tournament>()
            .WithMany()
            .HasForeignKey(group => group.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TournamentGroup>().HasIndex(group => new { group.TournamentId, group.Name }).IsUnique();

        modelBuilder.Entity<TournamentTeam>()
            .HasOne<Tournament>().WithMany()
            .HasForeignKey(team => team.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TournamentTeam>()
            .HasIndex(team => new { team.TournamentId, team.Name }).IsUnique();
        // Supports the global team list, which sorts by name across tournaments.
        modelBuilder.Entity<TournamentTeam>()
            .HasIndex(team => new { team.Name, team.Id });

        // Historical snapshots deliberately have no foreign keys to live entities.
        modelBuilder.Entity<TournamentChampion>()
            .HasIndex(champion => champion.TournamentId).IsUnique();
        modelBuilder.Entity<TournamentChampion>()
            .HasIndex(champion => new { champion.CompletedAtUtc, champion.Id });
        modelBuilder.Entity<TournamentChampion>()
            .Property(champion => champion.CompletedAtUtc)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        modelBuilder.Entity<TournamentMatch>()
            .HasOne<TournamentGroup>()
            .WithMany()
            .HasForeignKey(match => match.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
        // Keep old stub match rows readable; operational rows always store TournamentId.
        modelBuilder.Entity<TournamentMatch>().HasIndex(match => new { match.TournamentId, match.Round });
        modelBuilder.Entity<TournamentMatch>().HasIndex(match => match.HomeTeamId);
        modelBuilder.Entity<TournamentMatch>().HasIndex(match => match.VisitorTeamId);
        modelBuilder.Entity<OutboxEvent>().Property(message => message.OccurredAtUtc)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        modelBuilder.Entity<ConsumedEvent>().Property(message => message.ConsumedAtUtc)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
