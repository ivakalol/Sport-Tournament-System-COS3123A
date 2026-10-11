using System.ComponentModel.DataAnnotations.Schema;

namespace SportTournaments.Tournament.Domain;

[Table("Winners")]
public class Winner
{
    public int Id { get; set; }
    public int TournamentId { get; set; }
    // ParticipantId belongs to another database, so there is no cross-database FK.
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int Place { get; set; }
    public int Points { get; set; }
}
