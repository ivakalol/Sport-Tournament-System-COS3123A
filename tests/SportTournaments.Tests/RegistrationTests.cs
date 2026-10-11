using SportTournaments.Registration;
using SportTournaments.Registration.Domain;
using SportTournaments.Registration.Dtos;
using RegistrationEntity = SportTournaments.Registration.Domain.Registration;

namespace SportTournaments.Tests;

public class RegistrationTests
{
    private static readonly DateTime Start = DateTime.Parse("2030-01-02T10:00:00Z").ToUniversalTime();
    private static readonly DateTime End = Start.AddHours(2);
    private static object Tournament(int id = 1, string status = "Upcoming", int capacity = 3)
        => new { Id = id, Name = "Cup", Type = "Football", StartDate = Start, EndDate = End, Capacity = capacity, Status = status };
    private static RegistrationService Service(AppDbContext context, Func<string, object?>? tournaments = null, bool participantExists = true)
        => new(context, new TournamentApiClient(new StubHttpHandler(tournaments ?? (_ => Tournament())).CreateClient()),
            new ParticipantApiClient(new StubHttpHandler(_ => participantExists ? new { Id = 1, Name = "Alex" } : null).CreateClient()), new FixedTime());

    [Fact]
    public async Task Create_persists_snapshot_and_duplicate_is_rejected()
    {
        using var db = new TestDatabase<AppDbContext>();
        var service = Service(db.Context);
        var request = new RegistrationRequest { ParticipantId = 1, TournamentId = 1 };
        var result = await service.CreateAsync(request);
        Assert.Equal("Cup", result.TournamentName);
        Assert.Equal("Alex", result.ParticipantName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
        Assert.Single(db.Context.Registrations);
    }

    [Theory]
    [InlineData("Finished")]
    [InlineData("Canceled")]
    public async Task Create_rejects_non_upcoming_status(string status)
    {
        using var db = new TestDatabase<AppDbContext>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db.Context, _ => Tournament(status:status))
            .CreateAsync(new RegistrationRequest { ParticipantId=1, TournamentId=1 }));
        Assert.Empty(db.Context.Registrations);
    }

    [Fact]
    public async Task Create_rejects_missing_participant_and_missing_tournament()
    {
        using var db = new TestDatabase<AppDbContext>();
        var request = new RegistrationRequest { ParticipantId=1, TournamentId=1 };
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db.Context, participantExists:false).CreateAsync(request));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db.Context, _ => null).CreateAsync(request));
        Assert.Empty(db.Context.Registrations);
    }

    [Fact]
    public async Task Upcoming_tournament_that_has_already_started_rejects_signup()
    {
        using var db = new TestDatabase<AppDbContext>();
        var service = Service(db.Context, _ => new { Id = 1, Name = "Started", Type = "Football",
            StartDate = Start.AddYears(-1), EndDate = End, Capacity = 3, Status = "Upcoming" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new RegistrationRequest { ParticipantId = 1, TournamentId = 1 }));
        Assert.Empty(db.Context.Registrations);
    }

    [Fact]
    public async Task Full_tournament_rejects_signup()
    {
        using var db = new TestDatabase<AppDbContext>();
        db.Context.Registrations.Add(new RegistrationEntity { TournamentId=1, ParticipantId=2, StartDate=Start, EndDate=End });
        await db.Context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db.Context, _ => Tournament(capacity:1))
            .CreateAsync(new RegistrationRequest { ParticipantId=1, TournamentId=1 }));
        Assert.Single(db.Context.Registrations);
    }

    [Theory]
    [InlineData("Upcoming", true)]
    [InlineData("Finished", true)]
    [InlineData("Canceled", false)]
    [InlineData("Deleted", false)]
    public async Task Overlap_is_blocked_except_for_canceled_or_deleted_events(string previousStatus, bool blocked)
    {
        using var db = new TestDatabase<AppDbContext>();
        db.Context.Registrations.Add(new RegistrationEntity { TournamentId=2, ParticipantId=1, StartDate=Start, EndDate=End });
        await db.Context.SaveChangesAsync();
        var service = Service(db.Context, path => path.EndsWith("/2")
            ? previousStatus == "Deleted" ? null : Tournament(2, previousStatus) : Tournament());
        var request = new RegistrationRequest { ParticipantId=1, TournamentId=1 };
        if (blocked) await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
        else await service.CreateAsync(request);
        Assert.Equal(blocked ? 1 : 2, db.Context.Registrations.Count());
    }

    [Fact]
    public async Task Dependency_failure_leaves_no_registration()
    {
        using var db = new TestDatabase<AppDbContext>();
        await Assert.ThrowsAsync<HttpRequestException>(() => Service(db.Context, _ => new HttpRequestException("Offline"))
            .CreateAsync(new RegistrationRequest { ParticipantId=1, TournamentId=1 }));
        Assert.Empty(db.Context.Registrations);
    }

    [Fact]
    public async Task Participation_count_includes_only_finished_events()
    {
        using var db = new TestDatabase<AppDbContext>();
        db.Context.Registrations.AddRange(Enumerable.Range(1,3).Select(id => new RegistrationEntity { TournamentId=id, ParticipantId=1 }));
        await db.Context.SaveChangesAsync();
        var service = Service(db.Context, path => Tournament(int.Parse(path.Split('/').Last()),
            path.EndsWith("/1") ? "Finished" : path.EndsWith("/2") ? "Upcoming" : "Canceled"));
        var statistics = await service.GetStatisticsAsync(1);
        Assert.Equal(1, statistics.ParticipationCount);
        Assert.Equal(1, statistics.UpcomingRegistrations);
        Assert.Equal(3, statistics.TotalRegistrations);
    }

    [Theory]
    [InlineData(0, 2, 1, 3, true)]
    [InlineData(0, 2, 0, 2, true)]
    [InlineData(0, 4, 1, 2, true)]
    [InlineData(1, 2, 0, 4, true)]
    [InlineData(0, 2, 2, 4, false)]
    [InlineData(2, 4, 0, 2, false)]
    public void Overlap_handles_containment_equality_and_adjacent_intervals(int s, int e, int os, int oe, bool overlaps)
        => Assert.Equal(overlaps, RegistrationService.Overlaps(Start.AddHours(s), Start.AddHours(e), Start.AddHours(os), Start.AddHours(oe)));
}
