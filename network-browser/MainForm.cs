using System.ComponentModel;
using System.Diagnostics;

namespace ParadiseNetworkBrowser;

public sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(10, 14, 12);
    private static readonly Color PanelColor = Color.FromArgb(19, 27, 22);
    private static readonly Color BorderColor = Color.FromArgb(43, 61, 49);
    private static readonly Color AccentColor = Color.FromArgb(46, 204, 113);
    private static readonly Color TextColor = Color.FromArgb(230, 238, 232);
    private static readonly Color MutedColor = Color.FromArgb(148, 166, 153);

    private readonly DirectoryClient _client = new();
    private readonly PatchCatalogClient _patchCatalogClient = new();
    private readonly TextBox _directoryUrl = new() { Text = DirectoryClient.DefaultBaseUrl };
    private readonly TextBox _search = new() { PlaceholderText = "Search name, region, map, or address" };
    private readonly ComboBox _softwareFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _typeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _grid = new();
    private readonly Label _status = new() { Text = "Ready to connect to Paradise Directory" };
    private readonly Label _count = new() { Text = "0 servers" };
    private readonly Label _detailTitle = new() { Text = "Select a server", Font = new Font("Segoe UI Semibold", 13, FontStyle.Bold) };
    private readonly Label _details = new() { Text = "Choose a server to view its public details.", AutoSize = false };
    private readonly Button _refresh = new() { Text = "Refresh servers", Width = 132, Height = 36 };
    private readonly Button _patchCatalog = new() { Text = "Patch catalogue", Width = 132, Height = 36 };
    private readonly Button _copyEndpoint = new() { Text = "Copy endpoint", Width = 124, Height = 34, Enabled = false };
    private readonly Button _openDirectory = new() { Text = "Open directory", Width = 124, Height = 34 };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 30_000 };
    private readonly BindingList<ServerEntry> _servers = [];
    private CancellationTokenSource? _refreshCancellation;
    private bool _refreshing;

    public MainForm()
    {
        Text = "Paradise Network Browser";
        MinimumSize = new Size(1040, 650);
        Size = new Size(1260, 790);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BackgroundColor;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9.5f);
        BuildLayout();
        ApplyTheme(this);
        _refresh.Click += async (_, _) => await RefreshServersAsync();
        _patchCatalog.Click += async (_, _) => await ShowPatchCatalogAsync();
        _search.TextChanged += (_, _) => ApplyFilters();
        _softwareFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _typeFilter.SelectedIndexChanged += (_, _) => ApplyFilters();
        _grid.SelectionChanged += (_, _) => UpdateSelection();
        _copyEndpoint.Click += (_, _) => CopySelectedEndpoint();
        _openDirectory.Click += (_, _) => OpenDirectory();
        _timer.Tick += async (_, _) => await RefreshServersAsync();
        Shown += async (_, _) => await RefreshServersAsync();
        FormClosed += (_, _) =>
        {
            _timer.Stop();
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _client.Dispose();
            _patchCatalogClient.Dispose();
        };
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(22), BackColor = BackgroundColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "PARADISE NETWORK", Font = new Font("Segoe UI Semibold", 21, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(0, 0) };
        var subtitle = new Label { Text = "Public community servers and opted-in network nodes", ForeColor = MutedColor, AutoSize = true, Location = new Point(2, 42) };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var connection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, BackColor = PanelColor, Padding = new Padding(12), Margin = new Padding(0, 0, 0, 12) };
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        connection.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        connection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var urlLabel = new Label { Text = "DIRECTORY API", ForeColor = MutedColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        connection.Controls.Add(urlLabel, 0, 0);
        connection.Controls.Add(_directoryUrl, 0, 1);
        connection.Controls.Add(_refresh, 1, 1);
        connection.Controls.Add(_patchCatalog, 2, 1);
        connection.Controls.Add(_openDirectory, 3, 1);
        var hint = new Label { Text = "Auto-refresh every 30 seconds", ForeColor = MutedColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        connection.Controls.Add(hint, 4, 1);
        root.Controls.Add(connection, 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 4) };
        _search.Width = 340;
        _softwareFilter.Width = 150;
        _softwareFilter.Items.AddRange(["All software", "Paradise", "NekoNexus", "Other"]);
        _softwareFilter.SelectedIndex = 0;
        _typeFilter.Width = 150;
        _typeFilter.Items.AddRange(["All node types", "Game servers", "Community nodes"]);
        _typeFilter.SelectedIndex = 0;
        filters.Controls.Add(_search);
        filters.Controls.Add(_softwareFilter);
        filters.Controls.Add(_typeFilter);
        filters.Controls.Add(_count);
        _count.Padding = new Padding(12, 8, 0, 0);
        body.Controls.Add(filters, 0, 0);

        ConfigureGrid();
        body.Controls.Add(_grid, 0, 1);

        var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Padding = new Padding(14), Margin = new Padding(0, 12, 0, 0) };
        _detailTitle.Location = new Point(14, 12);
        _detailTitle.AutoSize = true;
        _details.Location = new Point(16, 42);
        _details.Size = new Size(760, 68);
        _details.ForeColor = MutedColor;
        _copyEndpoint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _copyEndpoint.Location = new Point(Width - 320, 16);
        _openDirectory.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        detailPanel.Controls.Add(_detailTitle);
        detailPanel.Controls.Add(_details);
        detailPanel.Controls.Add(_copyEndpoint);
        body.Controls.Add(detailPanel, 0, 2);
        root.Controls.Add(body, 0, 2);

        _status.Dock = DockStyle.Fill;
        _status.ForeColor = MutedColor;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        root.Controls.Add(_status, 0, 3);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = PanelColor;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = BorderColor;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowTemplate.Height = 34;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SERVER", DataPropertyName = nameof(ServerEntry.Name), FillWeight = 155 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ADDRESS", DataPropertyName = nameof(ServerEntry.Endpoint), FillWeight = 125 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SOFTWARE", DataPropertyName = nameof(ServerEntry.ServerSoftware), FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TYPE", DataPropertyName = nameof(ServerEntry.UsageType), FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PLAYERS", DataPropertyName = nameof(ServerEntry.PlayerCount), FillWeight = 65 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "REGION", DataPropertyName = nameof(ServerEntry.Region), FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MAP", DataPropertyName = nameof(ServerEntry.CurrentMap), FillWeight = 90 });
        _grid.DataSource = _servers;
    }

    private async Task RefreshServersAsync()
    {
        if (_refreshing || IsDisposed) return;
        _refreshing = true;
        _refresh.Enabled = false;
        _refresh.Text = "Refreshing...";
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        try
        {
            var url = _directoryUrl.Text.Trim();
            var entries = await _client.GetServersAsync(url, _refreshCancellation.Token);
            _servers.RaiseListChangedEvents = false;
            _servers.Clear();
            foreach (var entry in entries) _servers.Add(entry);
            _servers.RaiseListChangedEvents = true;
            _servers.ResetBindings();
            ApplyFilters();
            _status.Text = $"Connected | Last refreshed {DateTime.Now:HH:mm:ss} | {entries.Count} publicly listed online server(s)";
            _status.ForeColor = AccentColor;
            _timer.Start();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _status.Text = $"Directory unavailable: {ex.Message}";
            _status.ForeColor = Color.FromArgb(255, 170, 110);
        }
        finally
        {
            _refreshing = false;
            _refresh.Enabled = true;
            _refresh.Text = "Refresh servers";
        }
    }

    private async Task ShowPatchCatalogAsync()
    {
        _patchCatalog.Enabled = false;
        _patchCatalog.Text = "Loading...";
        try
        {
            var entries = await _patchCatalogClient.GetEntriesAsync(CancellationToken.None);
            if (entries.Count == 0)
            {
                MessageBox.Show(this,
                    "The central catalogue is reachable, but it contains no enabled providers or patch definitions.",
                    "Paradise patch catalogue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _status.Text = "Patch catalogue loaded | No enabled entries";
                return;
            }

            var lines = entries.Select(entry =>
            {
                var description = string.IsNullOrWhiteSpace(entry.Description) ? "No description provided." : entry.Description;
                var manifest = string.IsNullOrWhiteSpace(entry.Url) ? "" : $"\\r\\nManifest: {entry.Url}";
                return $"{entry.Name} [{entry.Kind}]\\r\\n{description}{manifest}";
            });

            MessageBox.Show(this, string.Join("\\r\\n\\r\\n", lines),
                $"Paradise patch catalogue ({entries.Count})",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            _status.Text = $"Patch catalogue loaded | {entries.Count} entry/entries | Read-only listing; no patches were applied";
            _status.ForeColor = AccentColor;
        }
        catch (Exception ex)
        {
            _status.Text = $"Patch catalogue unavailable: {ex.Message}";
            _status.ForeColor = Color.FromArgb(255, 170, 110);
            MessageBox.Show(this,
                $"Could not load the central patch catalogue. The directory may not publish it yet.\\r\\n\\r\\n{ex.Message}",
                "Paradise patch catalogue unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _patchCatalog.Enabled = true;
            _patchCatalog.Text = "Patch catalogue";
        }
    }

    private void ApplyFilters()
    {
        var query = _search.Text.Trim();
        var software = _softwareFilter.SelectedItem?.ToString() ?? "All software";
        var type = _typeFilter.SelectedItem?.ToString() ?? "All node types";
        var filtered = _servers.Where(s =>
            (string.IsNullOrWhiteSpace(query) ||
             $"{s.Name} {s.Address} {s.Region} {s.CurrentMap} {s.GameMode}".Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (software == "All software" ||
             (software == "Paradise" && s.ServerSoftware.Equals("paradise", StringComparison.OrdinalIgnoreCase)) ||
             (software == "NekoNexus" && s.ServerSoftware.Equals("nekonexus", StringComparison.OrdinalIgnoreCase)) ||
             (software == "Other" && s.ServerSoftware.Equals("other", StringComparison.OrdinalIgnoreCase))) &&
            (type == "All node types" ||
             (type == "Game servers" && s.UsageType.Equals("game", StringComparison.OrdinalIgnoreCase)) ||
             (type == "Community nodes" && s.UsageType.Equals("comm", StringComparison.OrdinalIgnoreCase)))
        ).ToList();

        _grid.DataSource = new BindingList<ServerEntry>(filtered);
        _count.Text = $"{filtered.Count} / {_servers.Count} servers";
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_grid.CurrentRow?.DataBoundItem is not ServerEntry server)
        {
            _detailTitle.Text = "Select a server";
            _details.Text = "Only publicly listed records returned by the directory are shown.";
            _copyEndpoint.Enabled = false;
            return;
        }

        _detailTitle.Text = server.Name;
        _details.Text = $"Endpoint: {server.Endpoint}\r\nStatus: {server.Status} | Players: {server.PlayerCount}/{server.MaxPlayers} | Region: {EmptyAsUnknown(server.Region)}\r\nSoftware: {server.ServerSoftware} {server.SoftwareVersion} | Protocol: {EmptyAsUnknown(server.ProtocolVersion)} | Map: {EmptyAsUnknown(server.CurrentMap)}";
        _copyEndpoint.Enabled = true;
    }

    private void CopySelectedEndpoint()
    {
        if (_grid.CurrentRow?.DataBoundItem is ServerEntry server)
        {
            Clipboard.SetText(server.Endpoint);
            _status.Text = $"Copied {server.Endpoint} to clipboard. Use the game's supported connection flow to join.";
        }
    }

    private void OpenDirectory()
    {
        if (Uri.TryCreate(_directoryUrl.Text.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }

    private static string EmptyAsUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;

    private static void ApplyTheme(Control control)
    {
        foreach (Control child in control.Controls)
        {
            child.ForeColor = child is Button ? Color.White : TextColor;
            if (child is TextBox or ComboBox)
            {
                child.BackColor = Color.FromArgb(25, 35, 29);
                child.ForeColor = TextColor;
            }
            else if (child is DataGridView grid)
            {
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 37, 29);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = AccentColor;
                grid.DefaultCellStyle.BackColor = PanelColor;
                grid.DefaultCellStyle.ForeColor = TextColor;
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 74, 46);
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(16, 23, 19);
            }
            else if (child is Button button)
            {
                button.BackColor = AccentColor;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = AccentColor;
                button.ForeColor = Color.FromArgb(7, 18, 10);
                button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
                button.Cursor = Cursors.Hand;
            }
            else if (child is Panel or TableLayoutPanel or FlowLayoutPanel)
            {
                child.BackColor = PanelColor;
            }
            ApplyTheme(child);
        }
    }
}
