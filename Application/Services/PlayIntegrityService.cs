namespace Application.Services;

using Application.Interfaces;
using Infrastructure.Configuration;
using Domain.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

public class PlayIntegrityService : IIntegrityService
{
    private const string BaseUrl =
        "https://playintegrity.googleapis.com/v1";

    private readonly HttpClient _http;
    private readonly PlayIntegrityOptions _options;
    private readonly ILogger<PlayIntegrityService> _logger;

    public PlayIntegrityService(
        HttpClient http,
        IOptions<PlayIntegrityOptions> options,
        ILogger<PlayIntegrityService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> VerifyAsync(string token)
    {
        // Skip enforcement in dev / when disabled in config
        if (!_options.Enforce)
        {
            _logger.LogWarning("Play Integrity enforcement is disabled.");
            return null;
        }

        try
        {
            var url = $"{BaseUrl}/{_options.PackageName}:decodeIntegrityToken";
            var response = await _http.PostAsJsonAsync(url, new { integrity_token = token });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Play Integrity API returned {Status}", response.StatusCode);
                return "Impossible de vérifier l'intégrité de l'appareil.";
            }

            var verdict = await response.Content
                .ReadFromJsonAsync<PlayIntegrityVerdict>();

            if (verdict is null)
                return "Réponse d'intégrité invalide.";

            return CheckVerdict(verdict.TokenPayloadExternal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Play Integrity verification failed");
            return null; // fail-open: let the request through on network error
        }
    }

    // ── Verdict checks ────────────────────────────────────────────────────────

    private static string? CheckVerdict(TokenPayloadExternal payload)
    {
        // 1. Device integrity
        var deviceVerdict = payload.DeviceIntegrity.DeviceRecognitionVerdict;
        if (!deviceVerdict.Contains("MEETS_DEVICE_INTEGRITY"))
            return "Appareil non fiable ou modifié.";

        // 2. Recent device activity (bot / farm detection)
        var activityLevel = payload.DeviceIntegrity
            .RecentDeviceActivity?.DeviceActivityLevel;

        if (activityLevel is "LEVEL_4")
            return "Activité suspecte détectée sur cet appareil.";

        // 3. Play Protect
        var playProtect = payload.EnvironmentDetails?.PlayProtectVerdict;
        if (playProtect is "HIGH_RISK")
            return "Play Protect a détecté des logiciels malveillants.";

        // 4. App access risk
        var appsDetected = payload.EnvironmentDetails?
            .AppAccessRiskVerdict?.AppsDetected ?? [];

        if (appsDetected.Contains("UNKNOWN_CAPTURING") ||
            appsDetected.Contains("UNKNOWN_CONTROLLING"))
            return "Une application suspecte est active sur l'appareil.";

        return null; // ✅ all checks passed
    }
}