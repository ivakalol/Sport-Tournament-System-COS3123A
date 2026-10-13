using System.ComponentModel.DataAnnotations;

namespace SportTournaments.Registration.Dtos;

public class RegistrationRequest
{
    [Range(1, int.MaxValue)] public int TournamentId { get; set; }
    [Range(1, int.MaxValue)] public int ParticipantId { get; set; }
}

public record RegistrationResponse(int Id, int TournamentId, int ParticipantId,
    string TournamentName, string ParticipantName, DateTime StartDate, DateTime EndDate, DateTime RegisteredAt);

public record RegistrationHistory(RegistrationResponse Registration, string TournamentStatus);

public record ParticipationStatistics(int ParticipationCount, int UpcomingRegistrations, int TotalRegistrations);
