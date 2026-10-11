using Microsoft.AspNetCore.Mvc;
using SportTournaments.Tournament.Domain;
using SportTournaments.Tournament.Dtos;

namespace SportTournaments.Tournament.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TournamentsController(TournamentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TournamentResponse>>> GetAll([FromQuery] TournamentStatus? status, [FromQuery] TournamentType? type, CancellationToken ct)
        => Ok(await service.GetAllAsync(status, type, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TournamentResponse>> GetById(int id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<TournamentResponse>> Create(TournamentRequest request, CancellationToken ct)
    {
        var tournament = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TournamentResponse>> Update(int id, TournamentRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<TournamentResponse>> Cancel(int id, CancellationToken ct)
        => Ok(await service.CancelAsync(id, ct));

    [HttpPost("{id:int}/finish")]
    public async Task<ActionResult<TournamentResponse>> Finish(int id, CancellationToken ct)
        => Ok(await service.FinishAsync(id, ct));

    [HttpPut("{id:int}/winners")]
    public async Task<ActionResult<List<WinnerResponse>>> SubmitWinners(int id, SubmitWinnersRequest request, CancellationToken ct)
        => Ok(await service.SubmitWinnersAsync(id, request, ct));

    [HttpGet("{id:int}/winners")]
    public async Task<ActionResult<List<WinnerResponse>>> GetWinners(int id, CancellationToken ct)
        => Ok(await service.GetWinnersAsync(id, ct));

    [HttpGet("{id:int}/participants")]
    public async Task<ActionResult<List<TournamentParticipantResponse>>> GetParticipants(int id, CancellationToken ct)
        => Ok(await service.GetParticipantsAsync(id, ct));
}
