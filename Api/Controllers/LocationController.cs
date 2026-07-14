namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/locations")]
[Authorize]
public class LocationController : BaseController
{
    private readonly ILocationService _locations;

    public LocationController(ILocationService locations) => _locations = locations;

    /// <summary>GET /api/locations</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LocationDto>>> GetAll()
        => Ok(await _locations.GetAllAsync());

    /// <summary>GET /api/locations/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LocationDto>> GetById(Guid id)
        => Ok(await _locations.GetByIdAsync(id));

    /// <summary>POST /api/locations — admin only</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LocationDto>> Create([FromBody] CreateLocationRequest request)
    {
        var location = await _locations.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = location.Id }, location);
    }

    /// <summary>PUT /api/locations/{id} — admin only</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LocationDto>> Update(Guid id, [FromBody] UpdateLocationRequest request)
        => Ok(await _locations.UpdateAsync(id, request));

    /// <summary>DELETE /api/locations/{id} — admin only</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _locations.DeleteAsync(id);
        return NoContent();
    }
}
