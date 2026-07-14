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
}
