namespace ParadiseNetworkBrowser;

public sealed class PatchCatalogEntry
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Url { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool Enabled { get; init; }
}
