namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/matches")]
[Authorize]
public class MatchController : BaseController
{
    private readonly IMatchService _matches;

    public MatchController(IMatchService matches) => _matches = matches;

    /// <summary>GET /api/matches</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetAll()
        => Ok(await _matches.GetAllAsync(CurrentUserId));

    /// <summary>GET /api/matches/by-division/{divisionId}</summary>
    [HttpGet("by-division/{divisionId:guid}")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetByDivision(Guid divisionId)
        => Ok(await _matches.GetByDivisionAsync(divisionId, CurrentUserId));

    /// <summary>GET /api/matches/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDto>> GetById(Guid id)
        => Ok(await _matches.GetByIdAsync(id, CurrentUserId));

    /// <summary>POST /api/matches — admin only</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<MatchDto>> Create([FromBody] CreateMatchRequest request)
    {
        var match = await _matches.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = match.Id }, match);
    }

    /// <summary>DELETE /api/matches/{id} — admin only</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _matches.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>POST /api/matches/{id}/subscribe</summary>
    [HttpPost("{id:guid}/subscribe")]
    public async Task<ActionResult<MatchDto>> Subscribe(Guid id, [FromBody] SubscribeRequest request)
        => Ok(await _matches.SubscribeAsync(id, CurrentUserId, request));

    /// <summary>POST /api/matches/{id}/unsubscribe</summary>
    [HttpPost("{id:guid}/unsubscribe")]
    public async Task<ActionResult<MatchDto>> Unsubscribe(Guid id)
        => Ok(await _matches.UnsubscribeAsync(id, CurrentUserId));

    /// <summary>POST /api/matches/{id}/confirm</summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<MatchDto>> Confirm(Guid id)
        => Ok(await _matches.ConfirmPresenceAsync(id, CurrentUserId));
}
