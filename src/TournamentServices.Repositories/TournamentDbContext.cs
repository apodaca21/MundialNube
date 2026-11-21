using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TournamentDbContext(DbContextOptions<TournamentDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentGroup> Groups => Set<TournamentGroup>();
    public DbSet<TournamentMatch> Matches => Set<TournamentMatch>();

    public DbSet<TournamentTeam> TournamentTeams => Set<TournamentTeam>();
    public DbSet<TournamentChampion> TournamentChampions => Set<TournamentChampion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

        modelBuilder.Entity<TournamentTeam>()
            .HasOne<Tournament>().WithMany()
            .HasForeignKey(team => team.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TournamentTeam>()
            .HasIndex(team => new { team.TournamentId, team.Name }).IsUnique();

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
    }
}
