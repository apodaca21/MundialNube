using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TournamentDbContext(DbContextOptions<TournamentDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentGroup> Groups => Set<TournamentGroup>();
    public DbSet<TournamentMatch> Matches => Set<TournamentMatch>();

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

        modelBuilder.Entity<TournamentMatch>()
            .HasOne<TournamentGroup>()
            .WithMany()
            .HasForeignKey(match => match.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
