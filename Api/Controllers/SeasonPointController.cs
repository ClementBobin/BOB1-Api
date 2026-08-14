namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/season-points")]
[Authorize]
public class SeasonPointController : BaseController
{
    private readonly ISeasonPointService _seasonPoints;

    public SeasonPointController(ISeasonPointService seasonPoints) => _seasonPoints = seasonPoints;

    /// <summary>GET /api/season-points/me — current user's points + total</summary>
    [HttpGet("me")]
    public async Task<ActionResult<SeasonPointSummaryDto>> GetMine()
        => Ok(await _seasonPoints.GetByUserAsync(CurrentUserId));

    /// <summary>GET /api/season-points/users/{userId} — admin only</summary>
    [HttpGet("users/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeasonPointSummaryDto>> GetByUser(Guid userId)
        => Ok(await _seasonPoints.GetByUserAsync(userId));

    /// <summary>GET /api/season-points — all entries, admin only</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<SeasonPointDto>>> GetAll()
        => Ok(await _seasonPoints.GetAllAsync());
}