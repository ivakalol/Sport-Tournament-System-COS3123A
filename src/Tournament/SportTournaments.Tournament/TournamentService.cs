using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SportTournaments.Tournament.Domain;
using SportTournaments.Tournament.Dtos;
using TournamentEntity = SportTournaments.Tournament.Domain.Tournament;

namespace SportTournaments.Tournament;

// Uses the database, registration API and clock.
public class TournamentService(AppDbContext context, RegistrationApiClient registrations, TimeProvider time)
{
    // Gets tournaments with optional filters.
    public async Task<List<TournamentResponse>> GetAllAsync(TournamentStatus? status, TournamentType? type, CancellationToken ct = default)
    {
        // Reject unknown filter values.
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new ArgumentException("Unknown status.");
        if (type.HasValue && !Enum.IsDefined(type.Value)) throw new ArgumentException("Unknown tournament type.");

        // Read without tracking changes.
        var query = context.Tournaments.AsNoTracking().AsQueryable();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (type.HasValue) query = query.Where(x => x.Type == type);

        // Sort and convert to responses.
        return (await query.OrderBy(x => x.StartDate).ThenBy(x => x.Id).ToListAsync(ct)).Select(ToResponse).ToList();
    }

    // Gets one tournament.
    public async Task<TournamentResponse> GetByIdAsync(int id, CancellationToken ct = default)
        => ToResponse(await FindAsync(id, ct));

    // Validates and saves a new tournament.
    public async Task<TournamentResponse> CreateAsync(TournamentRequest request, CancellationToken ct = default)
    {
        Validate(request, true);
        var tournament = new TournamentEntity();
        Apply(tournament, request);
        context.Tournaments.Add(tournament);
        await context.SaveChangesAsync(ct);
        return ToResponse(tournament);
    }

    // Updates an upcoming tournament.
    public async Task<TournamentResponse> UpdateAsync(int id, TournamentRequest request, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);
        RequireUpcoming(tournament);

        // Check whether the dates changed.
        var scheduleChanged = tournament.StartDate != NormalizeUtc(request.StartDate)
            || tournament.EndDate != NormalizeUtc(request.EndDate);
        Validate(request, scheduleChanged);

        // Keep enough room for registered participants.
        var entries = await registrations.GetTournamentRegistrationsAsync(id, ct);
        if (entries.Count > request.Capacity) throw new InvalidOperationException("Capacity cannot be less than the registration count.");
        // Keeping an occupied schedule fixed prevents edits from creating overlapping registrations.
        if (entries.Count > 0 && (scheduleChanged || tournament.Type != request.Type))
            throw new InvalidOperationException("Dates and sport cannot change after people have signed up. Cancel the tournament instead.");

        Apply(tournament, request);
        await context.SaveChangesAsync(ct);
        return ToResponse(tournament);
    }

    // Deletes a tournament if allowed.
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);
        if (tournament.Status == TournamentStatus.Finished)
            throw new InvalidOperationException("Finished tournaments cannot be deleted.");
        if (tournament.Status == TournamentStatus.Upcoming
            && (await registrations.GetTournamentRegistrationsAsync(id, ct)).Count > 0)
            throw new InvalidOperationException("An Upcoming tournament with registrations can only be canceled.");
        context.Tournaments.Remove(tournament);
        await context.SaveChangesAsync(ct);
    }

    // Cancels the tournament and clears its winners.
    public async Task<TournamentResponse> CancelAsync(int id, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);

        // Already canceled? Return it as it is.
        if (tournament.Status == TournamentStatus.Canceled) return ToResponse(tournament);
        RequireUpcoming(tournament);
        tournament.Status = TournamentStatus.Canceled;
        // Results of a canceled tournament are not official winners.
        await context.Entry(tournament).Collection(x => x.Winners).LoadAsync(ct);
        context.Winners.RemoveRange(tournament.Winners);
        await context.SaveChangesAsync(ct);
        return ToResponse(tournament);
    }

    // Validates and replaces the three winners.
    public async Task<List<WinnerResponse>> SubmitWinnersAsync(int id, SubmitWinnersRequest request, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);
        RequireUpcoming(tournament);

        // Require three different winners in places 1–3.
        if (request.Winners is null || request.Winners.Count != 3
            || request.Winners.Any(x => x is null || x.ParticipantId < 1 || x.Points < 0)
            || !request.Winners.Select(x => x.Place).Order().SequenceEqual(new[] { 1, 2, 3 })
            || request.Winners.Select(x => x.ParticipantId).Distinct().Count() != 3)
            throw new ArgumentException("Supply three distinct registered participants, places 1, 2 and 3, and non-negative points.");

        // Check that every winner signed up.
        var entries = await registrations.GetTournamentRegistrationsAsync(id, ct);
        if (request.Winners.Any(w => entries.All(r => r.ParticipantId != w.ParticipantId)))
            throw new InvalidOperationException("Every winner must be registered in this tournament.");
        // Replacement is atomic: the old set remains if validation or saving fails.
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        // Remove the previous winners.
        await context.Entry(tournament).Collection(x => x.Winners).LoadAsync(ct);
        context.Winners.RemoveRange(tournament.Winners);
        await context.SaveChangesAsync(ct);

        // Build the new winner records.
        tournament.Winners = request.Winners.Select(w => new Winner
        {
            TournamentId = id, ParticipantId = w.ParticipantId, Place = w.Place, Points = w.Points,
            ParticipantName = entries.Single(r => r.ParticipantId == w.ParticipantId).ParticipantName
        }).ToList();
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetWinnersAsync(id, ct);
    }

    // Finishes the tournament once three winners exist.
    public async Task<TournamentResponse> FinishAsync(int id, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);
        if (tournament.Status == TournamentStatus.Finished) return ToResponse(tournament);
        RequireUpcoming(tournament);
        if (await context.Winners.CountAsync(x => x.TournamentId == id, ct) != 3)
            throw new InvalidOperationException("Submit first, second and third place winners before finishing.");
        tournament.Status = TournamentStatus.Finished;
        await context.SaveChangesAsync(ct);
        return ToResponse(tournament);
    }

    // Gets winners in place order.
    public async Task<List<WinnerResponse>> GetWinnersAsync(int id, CancellationToken ct = default)
    {
        var tournament = await FindAsync(id, ct);
        var winners = await context.Winners.AsNoTracking().Where(x => x.TournamentId == id).OrderBy(x => x.Place).ToListAsync(ct);

        // Match each place to its prize.
        return winners.Select(w => new WinnerResponse(w.ParticipantId, w.ParticipantName, w.Place, w.Points,
            w.Place == 1 ? tournament.FirstPrize : w.Place == 2 ? tournament.SecondPrize : tournament.ThirdPrize)).ToList();
    }

    // Gets participants and their participation counts.
    public async Task<List<TournamentParticipantResponse>> GetParticipantsAsync(int id, CancellationToken ct = default)
    {
        // Make sure the tournament exists.
        await FindAsync(id, ct);
        var entries = await registrations.GetTournamentRegistrationsAsync(id, ct);
        var result = new List<TournamentParticipantResponse>();
        foreach (var entry in entries)
        {
            var statistics = await registrations.GetStatisticsAsync(entry.ParticipantId, ct);
            result.Add(new TournamentParticipantResponse(entry.ParticipantId, entry.ParticipantName, statistics.ParticipationCount));
        }
        return result;
    }

    // Finds the tournament or throws an error.
    private async Task<TournamentEntity> FindAsync(int id, CancellationToken ct)
        => await context.Tournaments.FindAsync([id], ct) ?? throw new KeyNotFoundException("Tournament not found.");

    // Blocks changes to non-upcoming tournaments.
    private static void RequireUpcoming(TournamentEntity tournament)
    {
        if (tournament.Status != TournamentStatus.Upcoming)
            throw new InvalidOperationException("Only Upcoming tournaments can be changed, canceled or finished.");
    }

    // Checks the request values.
    private void Validate(TournamentRequest request, bool requireFutureStart)
    {
        // Check the request's validation attributes.
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ArgumentException(string.Join(" ", errors.Select(x => x.ErrorMessage)));

        // Reject blank required text.
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description)
            || string.IsNullOrWhiteSpace(request.Location)) throw new ArgumentException("Name, description and location are required.");

        // Require a future start when requested.
        if (requireFutureStart && request.StartDate <= time.GetUtcNow())
            throw new ArgumentException("StartDate must be in the future.");
        // PDF note is contradictory; interpreted as EndDate > StartDate.
        if (NormalizeUtc(request.EndDate) <= NormalizeUtc(request.StartDate)) throw new ArgumentException("EndDate must be greater than StartDate.");

        // Limit prizes to two decimal places.
        if (new[] { request.FirstPrize, request.SecondPrize, request.ThirdPrize }.Any(x => decimal.Round(x, 2) != x))
            throw new ArgumentException("USD prizes may contain at most two decimal places.");
    }

    // MySQL datetime(6) stores microseconds; normalize .NET ticks before comparing dates.
    private static DateTime NormalizeUtc(DateTimeOffset value)
        => new(value.UtcTicks / 10 * 10, DateTimeKind.Utc);

    // Copies request values into the tournament.
    private static void Apply(TournamentEntity tournament, TournamentRequest request)
    {
        tournament.Name = request.Name.Trim();
        tournament.Type = request.Type;
        tournament.Description = request.Description.Trim();
        tournament.Location = request.Location.Trim();
        tournament.StartDate = NormalizeUtc(request.StartDate);
        tournament.EndDate = NormalizeUtc(request.EndDate);
        tournament.Capacity = request.Capacity;
        tournament.FirstPrize = request.FirstPrize;
        tournament.SecondPrize = request.SecondPrize;
        tournament.ThirdPrize = request.ThirdPrize;
    }

    // Converts the database entity to a response.
    private static TournamentResponse ToResponse(TournamentEntity x)
        => new(x.Id, x.Name, x.Type, x.Description, x.StartDate, x.EndDate, x.Location, x.Capacity,
            x.FirstPrize, x.SecondPrize, x.ThirdPrize, x.Status);
}
