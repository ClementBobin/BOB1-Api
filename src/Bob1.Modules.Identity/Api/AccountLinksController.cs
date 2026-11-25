using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bob1.Modules.Identity.Api;

internal sealed record AccountLinkTokenRequest(string Token);
internal sealed record ResetPasswordWithLinkRequest(string Token, string NewPassword);
internal sealed record ChangeEmailWithLinkRequest(string Token, string NewEmail);
internal sealed record DeleteAccountWithLinkRequest(string Token, string ConfirmEmail);

/// <summary>
/// Public endpoints behind the links an admin copies to a person. The token is the credential: it travels in the request body,
/// never in a URL, so it stays out of access logs; a bad, expired or already used token always gets the same 404.
/// </summary>
[Route(ApiRoutes.V1 + "/auth/account-links")]
[AllowAnonymous]
[Tags("Auth")]
internal sealed class AccountLinksController(AccountLinkService svc) : ApiControllerBase
{
    /// <summary>What the link is for (PasswordReset / EmailChange / DataExport / AccountDeletion), the person's first name and a masked email.</summary>
    [HttpPost("inspect")]
    public async Task<IActionResult> Inspect(AccountLinkTokenRequest r, CancellationToken ct)
    {
        var result = await svc.InspectAsync(r.Token, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithLinkRequest r, CancellationToken ct)
    {
        var result = await svc.ResetPasswordAsync(r.Token, r.NewPassword, ct);
        return result.IsSuccess ? NoContent() : result.Error.ToProblem();
    }

    /// <summary>All the data held about the person, as JSON (the page saves it as a file). Uses the link up.</summary>
    [HttpPost("export-data")]
    public async Task<IActionResult> ExportData(AccountLinkTokenRequest r, CancellationToken ct)
    {
        var result = await svc.ExportDataAsync(r.Token, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Erases the account and its data for good. The person types their email to confirm.</summary>
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountWithLinkRequest r, CancellationToken ct)
    {
        var result = await svc.DeleteAccountAsync(r.Token, r.ConfirmEmail, ct);
        return result.IsSuccess ? NoContent() : result.Error.ToProblem();
    }

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail(ChangeEmailWithLinkRequest r, CancellationToken ct)
    {
        var result = await svc.ChangeEmailAsync(r.Token, r.NewEmail, ct);
        return result.IsSuccess ? NoContent() : result.Error.ToProblem();
    }
}
