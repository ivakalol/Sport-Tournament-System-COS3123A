using Microsoft.AspNetCore.Mvc;
using SportTournaments.Registration.Dtos;

namespace SportTournaments.Registration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RegistrationsController(RegistrationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RegistrationResponse>> Create(RegistrationRequest request, CancellationToken ct)
    {
        var registration = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = registration.Id }, registration);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RegistrationResponse>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpGet("tournament/{id:int}")]
    public async Task<ActionResult<List<RegistrationResponse>>> GetByTournament(int id, CancellationToken ct)
        => Ok(await service.GetByTournamentAsync(id, ct));

    [HttpGet("participant/{id:int}")]
    public async Task<ActionResult<List<RegistrationHistory>>> GetByParticipant(int id, CancellationToken ct)
        => Ok(await service.GetByParticipantAsync(id, ct));

    [HttpGet("participant/{id:int}/statistics")]
    public async Task<ActionResult<ParticipationStatistics>> GetStatistics(int id, CancellationToken ct)
        => Ok(await service.GetStatisticsAsync(id, ct));
}
