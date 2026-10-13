using System.Net;
using System.Net.Http.Json;

namespace SportTournaments.Registration;

public class ParticipantApiClient(HttpClient http)
{
    // Ask the Participant service for one person by ID.
    // The service returns a small ParticipantDto, or null if that person does not exist.
    public Task<ParticipantDto?> GetParticipantAsync(int id, CancellationToken ct = default)
        => GetAsync<ParticipantDto>($"/api/participants/{id}", ct);

    // Send a GET request and turn the JSON response into a C# object.
    private async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await http.GetAsync(route, ct);

        // A 404 means there is no participant with this ID.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;
        
        response.EnsureSuccessStatusCode();

        // The request succeeded, so read its JSON body.
        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new HttpRequestException("The Participant service returned an empty response.");
    }

    public record ParticipantDto(int Id, string Name);
}
