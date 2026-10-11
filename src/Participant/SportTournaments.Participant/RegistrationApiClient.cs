using System.Net;
using System.Net.Http.Json;

namespace SportTournaments.Participant;

// HTTP details stay in the service layer, as in the communication workshop.
public class RegistrationApiClient(HttpClient http)
{
    public async Task<StatisticsDto> GetStatisticsAsync(int participantId, CancellationToken ct = default)
        => await GetAsync<StatisticsDto>($"/api/registrations/participant/{participantId}/statistics", ct)
            ?? throw new HttpRequestException("Missing participant statistics.");

    private async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await http.GetAsync(route, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new HttpRequestException("The Registration service returned an empty response.");
    }

    public record StatisticsDto(int ParticipationCount, int UpcomingRegistrations, int TotalRegistrations);
}
