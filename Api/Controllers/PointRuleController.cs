namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/admin/point-rules")]
[Authorize(Roles = "Admin")]
public class PointRuleController : BaseController
{
    private readonly IPointRuleService _rules;

    public PointRuleController(IPointRuleService rules) => _rules = rules;

    /// <summary>GET /api/admin/point-rules</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PointRuleDto>>> GetAll()
        => Ok(await _rules.GetAllAsync());

    /// <summary>PUT /api/admin/point-rules/{id}</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PointRuleDto>> Update(Guid id, [FromBody] UpdatePointRuleRequest request)
        => Ok(await _rules.UpdateAsync(id, request));
}
