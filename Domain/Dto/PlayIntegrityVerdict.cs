namespace Domain.Dto;

using System.Text.Json.Serialization;

public record PlayIntegrityVerdict
{
    [JsonPropertyName("tokenPayloadExternal")]
    public TokenPayloadExternal TokenPayloadExternal { get; init; } = new();
}

public record TokenPayloadExternal
{
    [JsonPropertyName("deviceIntegrity")]
    public DeviceIntegrity DeviceIntegrity { get; init; } = new();

    [JsonPropertyName("environmentDetails")]
    public EnvironmentDetails? EnvironmentDetails { get; init; }
}

public record DeviceIntegrity
{
    [JsonPropertyName("deviceRecognitionVerdict")]
    public List<string> DeviceRecognitionVerdict { get; init; } = [];

    [JsonPropertyName("recentDeviceActivity")]
    public RecentDeviceActivity? RecentDeviceActivity { get; init; }
}

public record RecentDeviceActivity
{
    [JsonPropertyName("deviceActivityLevel")]
    public string DeviceActivityLevel { get; init; } = string.Empty;
}

public record EnvironmentDetails
{
    [JsonPropertyName("playProtectVerdict")]
    public string? PlayProtectVerdict { get; init; }

    [JsonPropertyName("appAccessRiskVerdict")]
    public AppAccessRiskVerdict? AppAccessRiskVerdict { get; init; }
}

public record AppAccessRiskVerdict
{
    [JsonPropertyName("appsDetected")]
    public List<string> AppsDetected { get; init; } = [];
}