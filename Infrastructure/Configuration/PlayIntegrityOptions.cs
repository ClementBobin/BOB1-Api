namespace Infrastructure.Configuration;

public class PlayIntegrityOptions
{
    /// <summary>
    /// Your Android app package name, e.g. "com.mirage.bob1".
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to enforce integrity checks. Set to false in development
    /// to allow emulators and sideloaded builds to pass.
    /// </summary>
    public bool Enforce { get; set; } = true;
}