using System.ComponentModel.DataAnnotations.Schema;

namespace SportTournaments.Registration.Domain;

[Table("Registrations")]
public class Registration
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    public int ParticipantId { get; set; }
    public DateTime RegisteredAt { get; set; }
    // Snapshots preserve readable history if a canceled tournament is later deleted.
    public string TournamentName { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
