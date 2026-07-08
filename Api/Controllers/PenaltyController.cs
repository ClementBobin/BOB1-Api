namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/penalties")]
[Authorize]
public class PenaltyController : BaseController
{
    private readonly IPenaltyService _penalties;

    public PenaltyController(IPenaltyService penalties) => _penalties = penalties;

    /// <summary>GET /api/penalties — own penalties for officials, all for admins</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PenaltyDto>>> GetAll()
    {
        var isAdmin = User.IsInRole("Admin");
        var penalties = isAdmin
            ? await _penalties.GetAllAsync()
            : await _penalties.GetByUserAsync(CurrentUserId);
        return Ok(penalties);
    }

    /// <summary>POST /api/penalties — admin only</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PenaltyDto>> Create([FromBody] CreatePenaltyRequest request)
    {
        var penalty = await _penalties.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), penalty);
    }
}
