namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/teams")]
[Authorize]
public class TeamController : BaseController
{
    private readonly ITeamService _teams;

    public TeamController(ITeamService teams) => _teams = teams;

    /// <summary>GET /api/teams</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll()
        => Ok(await _teams.GetAllAsync());

    /// <summary>GET /api/teams/by-division/{divisionId}</summary>
    [HttpGet("by-division/{divisionId:guid}")]
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetByDivision(Guid divisionId)
        => Ok(await _teams.GetByDivisionAsync(divisionId));

    /// <summary>GET /api/teams/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id)
        => Ok(await _teams.GetByIdAsync(id));

    /// <summary>POST /api/teams — admin only</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamDto>> Create([FromBody] CreateTeamRequest request)
    {
        var team = await _teams.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    /// <summary>PUT /api/teams/{id} — admin only</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamDto>> Update(Guid id, [FromBody] CreateTeamRequest request)
        => Ok(await _teams.UpdateAsync(id, request));

    /// <summary>DELETE /api/teams/{id} — admin only</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _teams.DeleteAsync(id);
        return NoContent();
    }
}
