using Microsoft.EntityFrameworkCore;

namespace SportTournaments.Registration.Domain;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registration = modelBuilder.Entity<Registration>();
        registration.HasIndex(x => new { x.TournamentId, x.ParticipantId }).IsUnique();
        registration.HasIndex(x => x.ParticipantId);
        registration.Property(x => x.TournamentName).HasMaxLength(150);
        registration.Property(x => x.ParticipantName).HasMaxLength(150);
        registration.Property(x => x.StartDate).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        registration.Property(x => x.EndDate).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        registration.Property(x => x.RegisteredAt).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
}
