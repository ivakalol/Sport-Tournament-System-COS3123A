using Microsoft.EntityFrameworkCore;
using SportTournaments.Registration.Domain;
using SportTournaments.Registration.Dtos;
using RegistrationEntity = SportTournaments.Registration.Domain.Registration;

namespace SportTournaments.Registration;

public class RegistrationService(
    AppDbContext context,
    TournamentApiClient tournaments,
    ParticipantApiClient participants,
    TimeProvider time)
{
    // Check rules before save.!!!
    public async Task<RegistrationResponse> CreateAsync(
        RegistrationRequest request,
        CancellationToken ct = default)
    {
        if (request.ParticipantId < 1 || request.TournamentId < 1)
            throw new ArgumentException("Positive IDs are required.");

        // If two people try to take the last place at the same time,
        // this lock makes their registrations run one after the other.
        await using var writeLock = new RegistrationWriteLock(context);
        await writeLock.AcquireAsync(ct);
        
        var participant = await participants.GetParticipantAsync(request.ParticipantId, ct)
            ?? throw new KeyNotFoundException("Participant not found.");

        var tournament = await tournaments.GetTournamentAsync(request.TournamentId, ct)
            ?? throw new KeyNotFoundException("Tournament not found.");
        
        if (tournament.Status != "Upcoming"
            || tournament.StartDate <= time.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException(
                "Signups require an Upcoming tournament which has not started.");

        if (await context.Registrations.AnyAsync(
                x => x.TournamentId == request.TournamentId
                  && x.ParticipantId == request.ParticipantId,
                ct))
            throw new InvalidOperationException(
                "The participant is already registered.");


        if (await context.Registrations.CountAsync(
                x => x.TournamentId == request.TournamentId,
                ct) >= tournament.Capacity)
            throw new InvalidOperationException("The tournament is full.");

        // Now look at this person's other registrations.
        // We only need to investigate events whose times overlap.
        var existing = await context.Registrations
            .AsNoTracking()
            .Where(x => x.ParticipantId == request.ParticipantId)
            .ToListAsync(ct);

        foreach (var entry in existing)
        {
            if (!Overlaps(
                    tournament.StartDate,
                    tournament.EndDate,
                    entry.StartDate,
                    entry.EndDate))
                continue;

            // The stored registration tells us the old event's time.
            var other = await tournaments.GetTournamentAsync(entry.TournamentId, ct);
            if (other is not null && other.Status != "Canceled")
                throw new InvalidOperationException(
                    "The participant already has a tournament in this time range.");
        }

        // All checks passed. Keep a copy of the names and dates
        // so this registration remains understandable as historical data.
        var registration = new RegistrationEntity
        {
            TournamentId = tournament.Id,
            ParticipantId = participant.Id,
            TournamentName = tournament.Name,
            ParticipantName = participant.Name,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            RegisteredAt = time.GetUtcNow().UtcDateTime
        };

        context.Registrations.Add(registration);
        await context.SaveChangesAsync(ct);

        // return DTO. not a db entity
        return ToResponse(registration);
    }

    // 10:00–12:00 overlaps 11:00–13:00.
    // An event starting exactly at 12:00 can follow one ending at 12:00. !!!!!!!!!!!!!!!!!
    public static bool Overlaps(
        DateTime start,
        DateTime end,
        DateTime otherStart,
        DateTime otherEnd)
        => start < otherEnd && otherStart < end;

    // Find one registration by its own ID.
    public async Task<RegistrationResponse> GetByIdAsync(
        int id,
        CancellationToken ct = default)
        => ToResponse(
            await context.Registrations.FindAsync([id], ct)
            ?? throw new KeyNotFoundException("Registration not found."));

    // Show everyone registered for a particular tournament.
    public async Task<List<RegistrationResponse>> GetByTournamentAsync(
        int id,
        CancellationToken ct = default)
        => (await context.Registrations
                .AsNoTracking()
                .Where(x => x.TournamentId == id)
                .OrderBy(x => x.Id)
                .ToListAsync(ct))
            .Select(ToResponse)
            .ToList();

    // Show one person's registration history, including the current
    // status of each tournament.
    public async Task<List<RegistrationHistory>> GetByParticipantAsync(
        int id,
        CancellationToken ct = default)
    {
        var entries = await context.Registrations
            .AsNoTracking()
            .Where(x => x.ParticipantId == id)
            .OrderBy(x => x.StartDate)
            .ToListAsync(ct);

        var result = new List<RegistrationHistory>();

        foreach (var entry in entries)
        {
            // Registration owns the enrollment; Tournament owns the status.
            // If the tournament was deleted, we label that historical entry "Deleted".
            var tournament = await tournaments.GetTournamentAsync(
                entry.TournamentId, ct);

            result.Add(new RegistrationHistory(
                ToResponse(entry),
                tournament?.Status ?? "Deleted"));
        }

        return result;
    }

    // Completed participation, future bookings, and total history
    // are three different numbers.
    public async Task<ParticipationStatistics> GetStatisticsAsync(
        int id,
        CancellationToken ct = default)
    {
        var history = await GetByParticipantAsync(id, ct);

        return new ParticipationStatistics(
            history.Count(x => x.TournamentStatus == "Finished"),
            history.Count(x => x.TournamentStatus == "Upcoming"),
            history.Count);
    }

    // Convert the stored entity into the shape returned by the API.
    private static RegistrationResponse ToResponse(RegistrationEntity x)
        => new(
            x.Id,
            x.TournamentId,
            x.ParticipantId,
            x.TournamentName,
            x.ParticipantName,
            x.StartDate,
            x.EndDate,
            x.RegisteredAt);
}
