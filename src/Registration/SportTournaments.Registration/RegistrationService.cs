using Microsoft.EntityFrameworkCore;
using SportTournaments.Registration.Domain;
using SportTournaments.Registration.Dtos;
using RegistrationEntity = SportTournaments.Registration.Domain.Registration;

namespace SportTournaments.Registration;

public class RegistrationService(AppDbContext context, TournamentApiClient tournaments,
    ParticipantApiClient participants, TimeProvider time)
{
    public async Task<RegistrationResponse> CreateAsync(RegistrationRequest request, CancellationToken ct = default)
    {
        if (request.ParticipantId < 1 || request.TournamentId < 1) throw new ArgumentException("Positive IDs are required.");
        await using var writeLock = new RegistrationWriteLock(context);
        await writeLock.AcquireAsync(ct);
        var participant = await participants.GetParticipantAsync(request.ParticipantId, ct)
            ?? throw new KeyNotFoundException("Participant not found.");
        var tournament = await tournaments.GetTournamentAsync(request.TournamentId, ct)
            ?? throw new KeyNotFoundException("Tournament not found.");
        if (tournament.Status != "Upcoming" || tournament.StartDate <= time.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("Signups require an Upcoming tournament which has not started.");
        if (await context.Registrations.AnyAsync(x => x.TournamentId == request.TournamentId && x.ParticipantId == request.ParticipantId, ct))
            throw new InvalidOperationException("The participant is already registered.");
        if (await context.Registrations.CountAsync(x => x.TournamentId == request.TournamentId, ct) >= tournament.Capacity)
            throw new InvalidOperationException("The tournament is full.");
        var existing = await context.Registrations.AsNoTracking().Where(x => x.ParticipantId == request.ParticipantId).ToListAsync(ct);
        foreach (var entry in existing)
        {
            if (!Overlaps(tournament.StartDate, tournament.EndDate, entry.StartDate, entry.EndDate)) continue;
            var other = await tournaments.GetTournamentAsync(entry.TournamentId, ct);
            // A canceled (or deleted canceled) event releases the participant's time slot.
            if (other is not null && other.Status != "Canceled")
                throw new InvalidOperationException("The participant already has a tournament in this time range.");
        }
        var registration = new RegistrationEntity
        {
            TournamentId = tournament.Id, ParticipantId = participant.Id,
            TournamentName = tournament.Name, ParticipantName = participant.Name,
            StartDate = tournament.StartDate, EndDate = tournament.EndDate,
            RegisteredAt = time.GetUtcNow().UtcDateTime
        };
        context.Registrations.Add(registration);
        await context.SaveChangesAsync(ct);
        return ToResponse(registration);
    }

    // Half-open intervals [start, end): back-to-back tournaments do not overlap.
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd)
        => start < otherEnd && otherStart < end;

    public async Task<RegistrationResponse> GetByIdAsync(int id, CancellationToken ct = default)
        => ToResponse(await context.Registrations.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Registration not found."));

    public async Task<List<RegistrationResponse>> GetByTournamentAsync(int id, CancellationToken ct = default)
        => (await context.Registrations.AsNoTracking().Where(x => x.TournamentId == id).OrderBy(x => x.Id).ToListAsync(ct))
            .Select(ToResponse).ToList();

    public async Task<List<RegistrationHistory>> GetByParticipantAsync(int id, CancellationToken ct = default)
    {
        var entries = await context.Registrations.AsNoTracking().Where(x => x.ParticipantId == id).OrderBy(x => x.StartDate).ToListAsync(ct);
        var result = new List<RegistrationHistory>();
        foreach (var entry in entries)
        {
            var tournament = await tournaments.GetTournamentAsync(entry.TournamentId, ct);
            result.Add(new RegistrationHistory(ToResponse(entry), tournament?.Status ?? "Deleted"));
        }
        return result;
    }

    public async Task<ParticipationStatistics> GetStatisticsAsync(int id, CancellationToken ct = default)
    {
        var history = await GetByParticipantAsync(id, ct);
        return new ParticipationStatistics(history.Count(x => x.TournamentStatus == "Finished"),
            history.Count(x => x.TournamentStatus == "Upcoming"), history.Count);
    }

    private static RegistrationResponse ToResponse(RegistrationEntity x)
        => new(x.Id, x.TournamentId, x.ParticipantId, x.TournamentName, x.ParticipantName,
            x.StartDate, x.EndDate, x.RegisteredAt);
}
