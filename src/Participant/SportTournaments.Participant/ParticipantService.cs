using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SportTournaments.Participant.Domain;
using SportTournaments.Participant.Dtos;
using ParticipantEntity = SportTournaments.Participant.Domain.Participant;

namespace SportTournaments.Participant;

public class ParticipantService(AppDbContext context, RegistrationApiClient registrations)
{
    public async Task<List<ParticipantResponse>> GetAllAsync(CancellationToken ct = default)
        => await context.Participants.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new ParticipantResponse(x.Id, x.Name)).ToListAsync(ct);

    public async Task<ParticipantResponse> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var participant = await context.Participants.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        return new ParticipantResponse(participant.Id, participant.Name);
    }

    public async Task<ParticipantResponse> CreateAsync(ParticipantRequest request, CancellationToken ct = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.");
        var participant = new ParticipantEntity { Name = request.Name.Trim() };
        context.Participants.Add(participant);
        await context.SaveChangesAsync(ct);
        return new ParticipantResponse(participant.Id, participant.Name);
    }

    public async Task<ParticipantStatistics> GetStatisticsAsync(int id, CancellationToken ct = default)
    {
        var participant = await GetByIdAsync(id, ct);
        var statistics = await registrations.GetStatisticsAsync(id, ct);
        return new ParticipantStatistics(id, participant.Name, statistics.ParticipationCount,
            statistics.UpcomingRegistrations, statistics.TotalRegistrations);
    }
}
