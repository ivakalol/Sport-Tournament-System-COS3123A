using System.ComponentModel.DataAnnotations;

namespace SportTournaments.Participant.Dtos;

public class ParticipantRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
}

public record ParticipantResponse(int Id, string Name);
public record ParticipantStatistics(int ParticipantId, string Name, int ParticipationCount, int UpcomingRegistrations, int TotalRegistrations);
