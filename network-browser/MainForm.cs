using System.ComponentModel;
using System.Diagnostics;

namespace ParadiseNetworkBrowser;

public sealed class MainForm : Form
{
    private static readonly Color BackgroundColor = Color.FromArgb(7, 11, 9);
    private static readonly Color PanelColor = Color.FromArgb(13, 20, 16);
    private static readonly Color SurfaceColor = Color.FromArgb(17, 27, 21);
    private static readonly Color BorderColor = Color.FromArgb(35, 58, 42);
    private static readonly Color AccentColor = Color.FromArgb(57, 255, 136);
    private static readonly Color AccentDimColor = Color.FromArgb(25, 78, 47);
    private static readonly Color TextColor = Color.FromArgb(231, 245, 235);
    private static readonly Color MutedColor = Color.FromArgb(137, 163, 145);

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
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(24, 18, 24, 16), BackColor = BackgroundColor, Margin = new Padding(0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        Controls.Add(root);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackgroundColor, Margin = new Padding(0, 0, 0, 12) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        var brand = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor, Padding = new Padding(14, 0, 0, 0) };
        brand.Controls.Add(new Panel { BackColor = AccentColor, Location = new Point(0, 8), Size = new Size(3, 52) });
        brand.Controls.Add(new Label { Text = "PARADISE  /  NETWORK", Font = new Font("Segoe UI Semibold", 21, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(14, 2) });
        brand.Controls.Add(new Label { Text = "COMMUNITY SERVER DISCOVERY  /  LIVE DIRECTORY", Font = new Font("Consolas", 8.5f), ForeColor = MutedColor, AutoSize = true, Location = new Point(17, 43) });
        header.Controls.Add(brand, 0, 0);
        var meta = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
        meta.Controls.Add(new Label { Text = "NETWORK STATUS", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = MutedColor, AutoSize = true, Location = new Point(48, 10) });
        meta.Controls.Add(new Label { Text = "PUBLIC DIRECTORY", Font = new Font("Consolas", 9, FontStyle.Bold), ForeColor = AccentColor, AutoSize = true, Location = new Point(48, 31) });
        meta.Controls.Add(new Panel { BackColor = AccentColor, Size = new Size(8, 8), Location = new Point(32, 34) });
        header.Controls.Add(meta, 1, 0);
        root.Controls.Add(header, 0, 0);

        var connection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, BackColor = PanelColor, Padding = new Padding(14, 9, 14, 9), Margin = new Padding(0, 0, 0, 10) };
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        connection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        connection.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
        connection.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        connection.Controls.Add(new Label { Text = "DIRECTORY API ENDPOINT", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = MutedColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _directoryUrl.Dock = DockStyle.Fill;
        _directoryUrl.Margin = new Padding(0, 1, 10, 0);
        connection.Controls.Add(_directoryUrl, 0, 1);
        connection.Controls.Add(_refresh, 1, 1);
        connection.Controls.Add(_patchCatalog, 2, 1);
        connection.Controls.Add(_openDirectory, 3, 1);
        connection.Controls.Add(new Label { Text = "AUTO-SYNC  /  30 SEC", Font = new Font("Consolas", 8, FontStyle.Bold), ForeColor = MutedColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 4, 1);
        root.Controls.Add(connection, 0, 1);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 3), BackColor = BackgroundColor, Margin = new Padding(0) };
        _search.Width = 330;
        _search.Height = 30;
        _search.Margin = new Padding(0, 0, 10, 0);
        _softwareFilter.Width = 150;
        _softwareFilter.Height = 30;
        _softwareFilter.Margin = new Padding(0, 0, 10, 0);
        _softwareFilter.Items.Clear();
        _softwareFilter.Items.AddRange(["All software", "Paradise", "NekoNexus", "Other"]);
        _softwareFilter.SelectedIndex = 0;
        _typeFilter.Width = 150;
        _typeFilter.Height = 30;
        _typeFilter.Margin = new Padding(0, 0, 14, 0);
        _typeFilter.Items.Clear();
        _typeFilter.Items.AddRange(["All node types", "Game servers", "Community nodes"]);
        _typeFilter.SelectedIndex = 0;
        _count.Font = new Font("Consolas", 9, FontStyle.Bold);
        _count.ForeColor = AccentColor;
        _count.AutoSize = true;
        _count.Padding = new Padding(8, 7, 0, 0);
        filters.Controls.Add(_search);
        filters.Controls.Add(_softwareFilter);
        filters.Controls.Add(_typeFilter);
        filters.Controls.Add(_count);
        root.Controls.Add(filters, 0, 2);

        ConfigureGrid();
        _grid.Margin = new Padding(0, 0, 0, 10);
        root.Controls.Add(_grid, 0, 3);

        var detailPanel = new Panel { Dock = DockStyle.Fill, BackColor = PanelColor, Padding = new Padding(16), Margin = new Padding(0, 4, 0, 8) };
        detailPanel.Controls.Add(new Panel { BackColor = AccentColor, Dock = DockStyle.Left, Width = 3, Margin = new Padding(0) });
        _detailTitle.Location = new Point(20, 13);
        _detailTitle.AutoSize = true;
        _detailTitle.Font = new Font("Segoe UI Semibold", 12, FontStyle.Bold);
        _detailTitle.ForeColor = AccentColor;
        _details.Location = new Point(21, 43);
        _details.Size = new Size(850, 72);
        _details.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _details.Font = new Font("Consolas", 8.5f);
        _details.ForeColor = MutedColor;
        _copyEndpoint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _copyEndpoint.Location = new Point(Width - 340, 14);
        _copyEndpoint.Size = new Size(132, 34);
        detailPanel.Controls.Add(_detailTitle);
        detailPanel.Controls.Add(_details);
        detailPanel.Controls.Add(_copyEndpoint);
        root.Controls.Add(detailPanel, 0, 4);

        _status.Dock = DockStyle.Fill;
        _status.ForeColor = MutedColor;
        _status.Font = new Font("Consolas", 8.5f);
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Padding = new Padding(2, 0, 0, 0);
        root.Controls.Add(_status, 0, 5);
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
        _grid.RowTemplate.Height = 38;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersHeight = 38;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _grid.DefaultCellStyle.Padding = new Padding(8, 0, 4, 0);
        _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 4, 0);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 8.5f, FontStyle.Bold);
        _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9.5f);
        _grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
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
                var manifest = string.IsNullOrWhiteSpace(entry.Url) ? "" : $"\r\nManifest: {entry.Url}";
                return $"{entry.Name} [{entry.Kind}]\r\n{description}{manifest}";
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
                $"Could not load the central patch catalogue. The directory may not publish it yet.\r\n\r\n{ex.Message}",
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
        control.ForeColor = TextColor;
        if (control is Form)
            control.BackColor = BackgroundColor;
        else if (control is DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = PanelColor;
            grid.GridColor = BorderColor;
            grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AccentColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AccentColor;
            grid.DefaultCellStyle.BackColor = PanelColor;
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = AccentDimColor;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(10, 16, 12);
            grid.AlternatingRowsDefaultCellStyle.ForeColor = TextColor;
            grid.RowHeadersDefaultCellStyle.BackColor = PanelColor;
            grid.RowHeadersDefaultCellStyle.ForeColor = MutedColor;
        }
        else if (control is TextBox textBox)
        {
            textBox.BackColor = SurfaceColor;
            textBox.ForeColor = TextColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }
        else if (control is ComboBox comboBox)
        {
            comboBox.BackColor = SurfaceColor;
            comboBox.ForeColor = TextColor;
            comboBox.FlatStyle = FlatStyle.Flat;
        }
        else if (control is Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.BackColor = SurfaceColor;
            button.ForeColor = AccentColor;
            button.Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(5, 0, 5, 0);
            if (button == _refresh)
            {
                button.BackColor = AccentColor;
                button.ForeColor = Color.FromArgb(5, 15, 9);
                button.FlatAppearance.BorderColor = AccentColor;
            }
            else if (button == _patchCatalog)
            {
                button.BackColor = AccentDimColor;
                button.ForeColor = AccentColor;
                button.FlatAppearance.BorderColor = Color.FromArgb(39, 112, 66);
            }
        }
        else if (control is Panel panel)
        {
            if (panel.BackColor != BackgroundColor && panel.BackColor != PanelColor)
                panel.BackColor = PanelColor;
        }
        else if (control is TableLayoutPanel or FlowLayoutPanel)
        {
            if (control.BackColor != BackgroundColor)
                control.BackColor = PanelColor;
        }

        foreach (Control child in control.Controls)
            ApplyTheme(child);
    }
}
}
