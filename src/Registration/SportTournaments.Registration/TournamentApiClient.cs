using System.Net;
using System.Net.Http.Json;

namespace SportTournaments.Registration;

// HTTP details stay in the service layer, as in the communication workshop.
public class TournamentApiClient(HttpClient http)
{
    public Task<TournamentDto?> GetTournamentAsync(int id, CancellationToken ct = default)
        => GetAsync<TournamentDto>($"/api/tournaments/{id}", ct);

    private async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await http.GetAsync(route, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new HttpRequestException("The Tournament service returned an empty response.");
    }

    public record TournamentDto(int Id, string Name, string Type, DateTime StartDate, DateTime EndDate, int Capacity, string Status);
}
