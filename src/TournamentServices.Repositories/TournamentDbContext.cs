using Microsoft.EntityFrameworkCore;
using TournamentServices.Domain;

namespace TournamentServices.Repositories;

public class TournamentDbContext(DbContextOptions<TournamentDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tournament>(tournament =>
        {
            tournament.HasKey(item => item.Id);
            tournament.OwnsOne(item => item.Format, format =>
            {
                format.Property(value => value.Type).IsRequired();
                format.Property(value => value.MaxGroups);
                format.Property(value => value.MaxTeamsPerGroup);
            });
            tournament.Navigation(item => item.Format).IsRequired();
            tournament.HasMany(item => item.Groups)
                .WithOne()
                .HasForeignKey(group => group.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            tournament.HasMany(item => item.Matches)
                .WithOne()
                .HasForeignKey(match => match.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(team =>
        {
            team.HasKey(item => item.Id);
            team.Property(item => item.Name).IsRequired();
        });

        modelBuilder.Entity<Group>(group =>
        {
            group.HasKey(item => item.Id);
            group.Property(item => item.Name).IsRequired();
            group.Property(item => item.TournamentId).IsRequired();
            group.PrimitiveCollection(item => item.TeamIds);
        });

        modelBuilder.Entity<Match>(match =>
        {
            match.HasKey(item => item.Id);
            match.Property(item => item.TournamentId).IsRequired();
            match.Property(item => item.HomeTeamId).IsRequired();
            match.Property(item => item.VisitorTeamId).IsRequired();
            match.Property(item => item.Stage).IsRequired().HasDefaultValue(MatchStages.Group);
            match.Ignore(item => item.WinnerTeamId);
            match.Ignore(item => item.LoserTeamId);
            match.Property(item => item.Winner).HasConversion<string>();
            match.OwnsOne(item => item.Score, score =>
            {
                score.Property(value => value.HomeTeamScore);
                score.Property(value => value.VisitorTeamScore);
            });
            match.Navigation(item => item.Score).IsRequired();
            match.HasOne<Group>()
                .WithMany()
                .HasForeignKey(item => item.GroupId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });
    }
}
