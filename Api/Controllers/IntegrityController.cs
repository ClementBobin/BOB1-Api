namespace Api.Controllers;

using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/integrity")]
public class IntegrityController(IIntegrityService integrityService)
    : BaseController
{
    /// <summary>Verifies a Play Integrity token sent by the Android client.</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
    {
        var error = await integrityService.VerifyAsync(request.Token);
        if (error is not null)
            return Unauthorized(new { message = error });

        return Ok(new { valid = true });
    }
}

public record VerifyRequest(string Token);