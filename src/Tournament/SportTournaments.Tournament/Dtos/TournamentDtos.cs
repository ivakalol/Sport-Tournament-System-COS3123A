using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SportTournaments.Tournament.Domain;

namespace SportTournaments.Tournament.Dtos;

public class TournamentRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [JsonRequired, EnumDataType(typeof(TournamentType))] public TournamentType Type { get; set; }
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    [JsonRequired] public DateTimeOffset StartDate { get; set; }
    [JsonRequired] public DateTimeOffset EndDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    [JsonRequired, Range(1, 100000)] public int Capacity { get; set; }
    [JsonRequired, Range(typeof(decimal), "0", "9999999999.99")] public decimal FirstPrize { get; set; }
    [JsonRequired, Range(typeof(decimal), "0", "9999999999.99")] public decimal SecondPrize { get; set; }
    [JsonRequired, Range(typeof(decimal), "0", "9999999999.99")] public decimal ThirdPrize { get; set; }
}

public record TournamentResponse(int Id, string Name, TournamentType Type, string Description,
    DateTime StartDate, DateTime EndDate, string Location, int Capacity,
    decimal FirstPrize, decimal SecondPrize, decimal ThirdPrize, TournamentStatus Status);

public class WinnerRequest
{
    [Range(1, int.MaxValue)] public int ParticipantId { get; set; }
    [Range(1, 3)] public int Place { get; set; }
    [Range(0, int.MaxValue)] public int Points { get; set; }
}

public class SubmitWinnersRequest
{
    [Required, MinLength(3), MaxLength(3)] public List<WinnerRequest> Winners { get; set; } = [];
}

public record WinnerResponse(int ParticipantId, string ParticipantName, int Place, int Points, decimal PrizeUsd);
public record TournamentParticipantResponse(int ParticipantId, string ParticipantName, int ParticipationCount);
