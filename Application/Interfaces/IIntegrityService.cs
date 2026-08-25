namespace Application.Interfaces;

public interface IIntegrityService
{
    /// <summary>
    /// Verifies a Play Integrity token issued by the Android client.
    /// Returns null if valid, or an error message if the device is untrusted.
    /// </summary>
    Task<string?> VerifyAsync(string token);
}