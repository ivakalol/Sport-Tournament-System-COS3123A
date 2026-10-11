using Microsoft.EntityFrameworkCore;

namespace SportTournaments.Tournament.Domain;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Winner> Winners => Set<Winner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tournament = modelBuilder.Entity<Tournament>();
        tournament.Property(x => x.Name).HasMaxLength(150);
        tournament.Property(x => x.Description).HasMaxLength(2000);
        tournament.Property(x => x.Location).HasMaxLength(200);
        tournament.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        tournament.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        tournament.Property(x => x.StartDate).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        tournament.Property(x => x.EndDate).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        tournament.Property(x => x.FirstPrize).HasPrecision(12, 2);
        tournament.Property(x => x.SecondPrize).HasPrecision(12, 2);
        tournament.Property(x => x.ThirdPrize).HasPrecision(12, 2);
        tournament.HasMany(x => x.Winners).WithOne().HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Winner>().HasIndex(x => new { x.TournamentId, x.Place }).IsUnique();
        modelBuilder.Entity<Winner>().HasIndex(x => new { x.TournamentId, x.ParticipantId }).IsUnique();
        modelBuilder.Entity<Winner>().Property(x => x.ParticipantName).HasMaxLength(150);
    }
}
