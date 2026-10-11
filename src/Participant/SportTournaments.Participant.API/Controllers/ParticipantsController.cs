using Microsoft.AspNetCore.Mvc;
using SportTournaments.Participant.Dtos;

namespace SportTournaments.Participant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticipantsController(ParticipantService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ParticipantResponse>>> GetAll(CancellationToken ct)
        => Ok(await service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ParticipantResponse>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ParticipantResponse>> Create(ParticipantRequest request, CancellationToken ct)
    {
        var participant = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = participant.Id }, participant);
    }

    [HttpGet("{id:int}/statistics")]
    public async Task<ActionResult<ParticipantStatistics>> GetStatistics(int id, CancellationToken ct)
        => Ok(await service.GetStatisticsAsync(id, ct));
}
