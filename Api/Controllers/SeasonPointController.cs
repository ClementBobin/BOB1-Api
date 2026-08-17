namespace Api.Controllers;

using Application.Interfaces;
using Domain.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/ranking")]
[Authorize]
public class SeasonPointController : BaseController
{
    private readonly ISeasonPointService _seasonPoints;

    public SeasonPointController(ISeasonPointService seasonPoints) => _seasonPoints = seasonPoints;

    /// <summary>GET /api/ranking — full leaderboard, visible to all authenticated users</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SeasonPointRankingDto>>> GetRanking(
        [FromQuery] int? season = null)
        => Ok(await _seasonPoints.GetRankingAsync(season));

    /// <summary>GET /api/ranking/me — current user's points breakdown</summary>
    [HttpGet("me")]
    public async Task<ActionResult<SeasonPointSummaryDto>> GetMine(
        [FromQuery] int? season = null)
        => Ok(await _seasonPoints.GetByUserAsync(CurrentUserId, season));

    /// <summary>GET /api/ranking/users/{userId} — specific user breakdown</summary>
    [HttpGet("users/{userId:guid}")]
    public async Task<ActionResult<SeasonPointSummaryDto>> GetByUser(
        Guid userId, [FromQuery] int? season = null)
        => Ok(await _seasonPoints.GetByUserAsync(userId, season));
}