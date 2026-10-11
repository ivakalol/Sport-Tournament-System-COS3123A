using System.ComponentModel.DataAnnotations.Schema;

namespace SportTournaments.Tournament.Domain;

public enum TournamentType { Basketball, Football, Swimming }
public enum TournamentStatus { Upcoming, Finished, Canceled }

[Table("Tournaments")]
public class Tournament
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TournamentType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal FirstPrize { get; set; }
    public decimal SecondPrize { get; set; }
    public decimal ThirdPrize { get; set; }
    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;
    public List<Winner> Winners { get; set; } = [];
}
