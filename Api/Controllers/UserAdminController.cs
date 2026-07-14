namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public class UserAdminController : BaseController
{
    private readonly IUserAdminService _users;

    public UserAdminController(IUserAdminService users) => _users = users;

    /// <summary>GET /api/admin/users</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
        => Ok(await _users.GetAllAsync());

    /// <summary>GET /api/admin/users/{id}</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
        => Ok(await _users.GetByIdAsync(id));

    /// <summary>PATCH /api/admin/users/{id}/role</summary>
    [HttpPatch("{id:guid}/role")]
    public async Task<ActionResult<UserDto>> ChangeRole(Guid id, [FromBody] ChangeUserRoleRequest request)
        => Ok(await _users.ChangeRoleAsync(id, request.Role));
}
