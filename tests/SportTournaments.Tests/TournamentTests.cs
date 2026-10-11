using SportTournaments.Tournament;
using SportTournaments.Tournament.Domain;
using SportTournaments.Tournament.Dtos;
using TournamentEntity = SportTournaments.Tournament.Domain.Tournament;

namespace SportTournaments.Tests;

public class TournamentTests
{
    private static TournamentRequest Request() => new()
    {
        Name = "AUBG Cup", Type = TournamentType.Basketball, Description = "Campus tournament",
        StartDate = DateTimeOffset.Parse("2030-01-02T10:00:00Z"),
        EndDate = DateTimeOffset.Parse("2030-01-02T12:00:00Z"), Location = "Gym", Capacity = 10,
        FirstPrize = 100, SecondPrize = 50, ThirdPrize = 25
    };

    private static TournamentService Service(AppDbContext db, object? registrations = null)
    {
        var handler = new StubHttpHandler(_ => registrations ?? Array.Empty<object>());
        return new TournamentService(db, new RegistrationApiClient(handler.CreateClient()), new FixedTime());
    }

    [Theory]
    [InlineData("past")]
    [InlineData("equalEnd")]
    [InlineData("earlierEnd")]
    [InlineData("zeroCapacity")]
    [InlineData("negativePrize")]
    [InlineData("prizePrecision")]
    [InlineData("invalidType")]
    [InlineData("blankName")]
    public async Task Create_rejects_invalid_tournament_without_writing(string invalid)
    {
        using var db = new TestDatabase<AppDbContext>();
        var request = Request();
        switch (invalid)
        {
            case "past": request.StartDate = DateTimeOffset.Parse("2029-01-01T00:00:00Z"); break;
            case "equalEnd": request.EndDate = request.StartDate; break;
            case "earlierEnd": request.EndDate = request.StartDate.AddMinutes(-1); break;
            case "zeroCapacity": request.Capacity = 0; break;
            case "negativePrize": request.FirstPrize = -1; break;
            case "prizePrecision": request.FirstPrize = 1.001m; break;
            case "invalidType": request.Type = (TournamentType)99; break;
            case "blankName": request.Name = "  "; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Service(db.Context).CreateAsync(request));
        Assert.Empty(db.Context.Tournaments);
    }

    [Fact]
    public async Task Location_edit_with_submicrosecond_input_does_not_change_occupied_schedule()
    {
        using var db = new TestDatabase<AppDbContext>();
        var entries = new[] { new { Id = 1, TournamentId = 1, ParticipantId = 5, ParticipantName = "Alex" } };
        var service = Service(db.Context, entries);
        var request = Request();
        request.StartDate = request.StartDate.AddTicks(7);
        request.EndDate = request.EndDate.AddTicks(7);
        var created = await service.CreateAsync(request);
        request.Location = "New gym";
        var updated = await service.UpdateAsync(created.Id, request);
        Assert.Equal(created.StartDate, updated.StartDate);
        Assert.Equal("New gym", updated.Location);
    }

    [Fact]
    public async Task Create_defaults_to_upcoming_and_update_retains_status()
    {
        using var db = new TestDatabase<AppDbContext>();
        var service = Service(db.Context);
        var tournament = await service.CreateAsync(Request());
        Assert.Equal(TournamentStatus.Upcoming, tournament.Status);
        var changed = Request(); changed.Location = "New gym";
        Assert.Equal("New gym", (await service.UpdateAsync(tournament.Id, changed)).Location);
    }

    [Theory]
    [InlineData(TournamentStatus.Finished)]
    [InlineData(TournamentStatus.Upcoming)]
    public async Task Delete_rejects_finished_or_occupied_upcoming(TournamentStatus status)
    {
        using var db = new TestDatabase<AppDbContext>();
        db.Context.Tournaments.Add(new TournamentEntity { Id = 1, Status = status });
        await db.Context.SaveChangesAsync();
        var entries = new[] { new { Id = 1, TournamentId = 1, ParticipantId = 5, ParticipantName = "Alex" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db.Context, entries).DeleteAsync(1));
        Assert.Single(db.Context.Tournaments);
    }

    [Fact]
    public async Task Occupied_tournament_cannot_change_dates_or_reduce_capacity_below_count()
    {
        using var db = new TestDatabase<AppDbContext>();
        var entries = new[] { new { Id = 1, TournamentId = 1, ParticipantId = 5, ParticipantName = "Alex" },
            new { Id = 2, TournamentId = 1, ParticipantId = 6, ParticipantName = "Boris" } };
        var service = Service(db.Context, entries);
        var tournament = await service.CreateAsync(Request());
        var changed = Request(); changed.StartDate = changed.StartDate.AddMinutes(10);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(tournament.Id, changed));
        changed = Request(); changed.Capacity = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(tournament.Id, changed));
    }

    [Fact]
    public async Task Finish_requires_valid_registered_winners_then_freezes_results()
    {
        using var db = new TestDatabase<AppDbContext>();
        var entries = Enumerable.Range(1, 3).Select(id => new { Id = id, TournamentId = 1, ParticipantId = id, ParticipantName = $"Person {id}" }).ToArray();
        var service = Service(db.Context, entries);
        var tournament = await service.CreateAsync(Request());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinishAsync(tournament.Id));
        var winners = new SubmitWinnersRequest { Winners = Enumerable.Range(1, 3)
            .Select(id => new WinnerRequest { ParticipantId = id, Place = id, Points = 100-id }).ToList() };
        Assert.Equal(100m, (await service.SubmitWinnersAsync(tournament.Id, winners))[0].PrizeUsd);
        // Replacing the provisional winner set exercises unique indexes and the transaction.
        winners.Winners[0].Points = 200;
        Assert.Equal(200, (await service.SubmitWinnersAsync(tournament.Id, winners))[0].Points);
        Assert.Equal(TournamentStatus.Finished, (await service.FinishAsync(tournament.Id)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitWinnersAsync(tournament.Id, winners));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(tournament.Id));
    }

    [Fact]
    public async Task Unregistered_or_duplicate_winners_do_not_replace_existing_results()
    {
        using var db = new TestDatabase<AppDbContext>();
        var service = Service(db.Context);
        var tournament = await service.CreateAsync(Request());
        var winners = new SubmitWinnersRequest { Winners = Enumerable.Range(1, 3)
            .Select(id => new WinnerRequest { ParticipantId = id, Place = id, Points = 10 }).ToList() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SubmitWinnersAsync(tournament.Id, winners));
        winners.Winners[2].ParticipantId = 1;
        await Assert.ThrowsAsync<ArgumentException>(() => service.SubmitWinnersAsync(tournament.Id, winners));
        Assert.Empty(db.Context.Winners);
    }

    [Fact]
    public async Task Cancel_is_idempotent_and_canceled_tournament_can_be_deleted()
    {
        using var db = new TestDatabase<AppDbContext>();
        var service = Service(db.Context);
        var tournament = await service.CreateAsync(Request());
        await service.CancelAsync(tournament.Id);
        Assert.Equal(TournamentStatus.Canceled, (await service.CancelAsync(tournament.Id)).Status);
        await service.DeleteAsync(tournament.Id);
        Assert.Empty(db.Context.Tournaments);
    }
}

