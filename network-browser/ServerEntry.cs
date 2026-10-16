using System.Text.Json.Serialization;

namespace ParadiseNetworkBrowser;

public sealed class ServerEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("address")] public string Address { get; set; } = "";
    [JsonPropertyName("port")] public int Port { get; set; }
    [JsonPropertyName("region")] public string Region { get; set; } = "";
    [JsonPropertyName("game_mode")] public string GameMode { get; set; } = "";
    [JsonPropertyName("current_map")] public string CurrentMap { get; set; } = "";
    [JsonPropertyName("player_count")] public int PlayerCount { get; set; }
    [JsonPropertyName("max_players")] public int MaxPlayers { get; set; }
    [JsonPropertyName("protocol_version")] public string ProtocolVersion { get; set; } = "";
    [JsonPropertyName("software_version")] public string SoftwareVersion { get; set; } = "";
    [JsonPropertyName("server_software")] public string ServerSoftware { get; set; } = "other";
    [JsonPropertyName("usage_type")] public string UsageType { get; set; } = "game";
    [JsonPropertyName("status")] public string Status { get; set; } = "unknown";
    [JsonPropertyName("last_successful_probe")] public string? LastSuccessfulProbe { get; set; }

    public string Endpoint => $"{Address}:{Port}";
}

public sealed class ServerListResponse
{
    [JsonPropertyName("servers")] public List<ServerEntry> Servers { get; set; } = [];
}
