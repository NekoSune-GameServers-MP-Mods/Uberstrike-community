using System.Net.Http;
using System.Text.Json;

namespace ParadiseNetworkBrowser;

public sealed class DirectoryClient : IDisposable
{
    public const string DefaultBaseUrl = "https://paradise-directory.nekosunevr.co.uk/";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<IReadOnlyList<ServerEntry>> GetServersAsync(string baseUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Enter a valid HTTP or HTTPS directory URL.");

        var endpoint = new Uri(baseUri, "/api/community/servers");
        using var response = await _http.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<ServerListResponse>(stream, JsonOptions, cancellationToken);
        return payload?.Servers ?? [];
    }

    public void Dispose() => _http.Dispose();
}
