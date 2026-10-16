using System.Xml;
using System.Xml.Linq;

namespace ParadiseNetworkBrowser;

public sealed class PatchCatalogClient : IDisposable
{
    public const string CatalogUrl = "https://paradise-directory.nekosunevr.co.uk/patch-file.xml";
    private const int MaxCatalogCharacters = 1_000_000;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<IReadOnlyList<PatchCatalogEntry>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CatalogUrl);
        request.Headers.UserAgent.ParseAdd("ParadiseNetworkBrowser/1.0");

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxCatalogCharacters)
            throw new InvalidDataException("The patch catalogue is larger than the 1 MB safety limit.");

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (xml.Length > MaxCatalogCharacters)
            throw new InvalidDataException("The patch catalogue is larger than the 1 MB safety limit.");

        using var input = new StringReader(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxCatalogCharacters,
            IgnoreComments = true
        });

        var document = XDocument.Load(reader, LoadOptions.None);
        var root = document.Root ?? throw new InvalidDataException("The patch catalogue has no XML root element.");

        if (root.Name.LocalName.Equals("PatchCatalog", StringComparison.OrdinalIgnoreCase))
        {
            return root.Elements()
                .Where(element => element.Name.LocalName.Equals("Provider", StringComparison.OrdinalIgnoreCase))
                .Select(ReadProvider)
                .Where(entry => entry.Enabled)
                .ToList();
        }

        // Support the existing UniversalUnityPatcher PatchDefinition shape as a
        // read-only catalogue. This does not apply or execute any patch.
        if (root.Name.LocalName.Equals("PatchDefinition", StringComparison.OrdinalIgnoreCase))
        {
            return root.Elements()
                .Where(element => element.Name.LocalName.Equals("Patch", StringComparison.OrdinalIgnoreCase))
                .Select(element => new PatchCatalogEntry
                {
                    Name = Attribute(element, "Name") ?? "Unnamed patch",
                    Description = Attribute(element, "ShortDescription") ?? "UniversalUnityPatcher patch definition",
                    Url = "",
                    Kind = "Patch definition",
                    Enabled = true
                })
                .ToList();
        }

        throw new InvalidDataException($"Unsupported patch catalogue format: <{root.Name.LocalName}>.");
    }

    private static PatchCatalogEntry ReadProvider(XElement element)
    {
        var name = Attribute(element, "Name") ?? Attribute(element, "Id") ?? "Unnamed provider";
        var url = Attribute(element, "ManifestUrl") ?? Attribute(element, "Url") ?? "";
        var description = Attribute(element, "Description") ?? "";
        var enabledText = Attribute(element, "Enabled");
        var enabled = !bool.TryParse(enabledText, out var parsed) || parsed;

        if (!string.IsNullOrWhiteSpace(url) &&
            (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidDataException($"Provider '{name}' has a manifest URL that is not a valid HTTPS URL.");

        return new PatchCatalogEntry
        {
            Name = name,
            Description = description,
            Url = url,
            Kind = "Patch provider",
            Enabled = enabled
        };
    }

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();

    public void Dispose() => _http.Dispose();
}
