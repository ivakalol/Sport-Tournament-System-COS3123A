using System.Net;
using System.Net.Http.Json;

namespace SportTournaments.Registration;

// HTTP details stay in the service layer, as in the communication workshop.
public class ParticipantApiClient(HttpClient http)
{
    public Task<ParticipantDto?> GetParticipantAsync(int id, CancellationToken ct = default)
        => GetAsync<ParticipantDto>($"/api/participants/{id}", ct);

    private async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await http.GetAsync(route, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new HttpRequestException("The Participant service returned an empty response.");
    }

    public record ParticipantDto(int Id, string Name);
}
