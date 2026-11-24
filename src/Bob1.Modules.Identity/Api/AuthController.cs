using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Identity.Api;

[Route(ApiRoutes.V1 + "/auth")]
[Tags("Auth")]
internal sealed class AuthController(AuthService svc, ICurrentUser user) : ApiControllerBase
{
    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest r, CancellationToken ct)
    {
        var result = await svc.RegisterAsync(r, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest r, CancellationToken ct)
    {
        var result = await svc.LoginAsync(r, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest r, CancellationToken ct)
    {
        var result = await svc.RefreshAsync(r, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Enroll this device for biometric sign-in (call while signed in). The response holds the only copy of the token.</summary>
    [HttpPost("biometric/enroll"), Authorize]
    public async Task<IActionResult> EnrollBiometric(BiometricEnrollRequest r, CancellationToken ct)
    {
        var result = await svc.EnrollBiometricAsync(user.UserId, r, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Sign in with a device's biometric token (the app only has it after a successful fingerprint / face check).</summary>
    [HttpPost("biometric/login"), AllowAnonymous]
    public async Task<IActionResult> LoginBiometric(BiometricLoginRequest r, CancellationToken ct)
    {
        var result = await svc.LoginBiometricAsync(r, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Turn biometric sign-in off for one device (its credential id) or for every device (no id).</summary>
    [HttpPost("biometric/disable"), Authorize]
    public async Task<IActionResult> DisableBiometric(BiometricDisableRequest r, CancellationToken ct)
    {
        await svc.DisableBiometricAsync(user.UserId, r.CredentialId, ct);
        return NoContent();
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(RefreshRequest r, CancellationToken ct)
    {
        await svc.LogoutAsync(r.RefreshToken, ct);
        return NoContent();
    }
}
