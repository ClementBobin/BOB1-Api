namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/divisions")]
[Authorize]
public class DivisionController : BaseController
{
    private readonly IDivisionService _divisions;

    public DivisionController(IDivisionService divisions) => _divisions = divisions;

    /// <summary>GET /api/divisions</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DivisionDto>>> GetAll()
        => Ok(await _divisions.GetAllAsync());

    /// <summary>POST /api/divisions — admin only</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DivisionDto>> Create([FromBody] CreateDivisionRequest request)
    {
        var division = await _divisions.CreateAsync(request);
        return CreatedAtAction(nameof(GetAll), division);
    }

    /// <summary>DELETE /api/divisions/{id} - admin only</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _divisions.DeleteAsync(id);
        return NoContent();
    }
}
