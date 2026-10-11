using System.ComponentModel.DataAnnotations.Schema;

namespace SportTournaments.Participant.Domain;

[Table("Participants")]
public class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
