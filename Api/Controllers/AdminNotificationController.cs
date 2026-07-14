namespace Api.Controllers;

using Application.Interfaces;

using Domain.Dto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
public class AdminNotificationController : BaseController
{
    private readonly IAdminNotificationService _admin;

    public AdminNotificationController(IAdminNotificationService admin) => _admin = admin;

    /// <summary>GET /api/admin/notifications — all admin-created notifications</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetAll()
        => Ok(await _admin.GetAllAsync());

    /// <summary>POST /api/admin/notifications — broadcast to All / Referes / specific users</summary>
    [HttpPost]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> Create(
        [FromBody] AdminCreateNotificationRequest request)
    {
        var created = await _admin.CreateAsync(request, CurrentUserId);
        return Ok(created);
    }

    /// <summary>PUT /api/admin/notifications/{id}</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> Update(
        Guid id, [FromBody] UpdateNotificationRequest request)
        => Ok(await _admin.UpdateAsync(id, request, CurrentUserId));

    /// <summary>DELETE /api/admin/notifications/{id}</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _admin.DeleteAsync(id, CurrentUserId);
        return NoContent();
    }
}
