using Microsoft.EntityFrameworkCore;

namespace SportTournaments.Participant.Domain;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Participant> Participants => Set<Participant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Participant>().Property(x => x.Name).HasMaxLength(150);
    }
}
