using Docker.DotNet;
using Docker.DotNet.Models;
using System.Net.NetworkInformation;
using System.Text;

namespace DockerManager;

public class MainForm : Form
{
    private readonly DockerClient _client;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    // UI Controls
    private DataGridView _containerGrid = null!;
    private Button _btnStart = null!;
    private Button _btnStop = null!;
    private Button _btnRestart = null!;
    private Button _btnDelete = null!;
    private Button _btnInstallMySQL = null!;
    private Button _btnInstallPostgres = null!;
    private TextBox _txtLogs = null!;
    private Label _lblStats = null!;
    private Label _lblCpu = null!;
    private Label _lblRam = null!;

    public MainForm()
    {
        _client = new DockerClientConfiguration(
            new Uri("npipe://./pipe/docker_engine"))
            .CreateClient();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _refreshTimer.Tick += async (s, e) => await RefreshContainerList();

        InitializeUI();
        _refreshTimer.Start();
    }

    private void InitializeUI()
    {
        Text = "Docker Manager";
        Size = new Size(1050, 720);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 600);

        // === Container DataGridView ===
        _containerGrid = new DataGridView
        {
            Location = new Point(12, 12),
            Size = new Size(700, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
        _containerGrid.Columns.Add("Id", "ID");
        _containerGrid.Columns.Add("Name", "Nazwa");
        _containerGrid.Columns.Add("Image", "Obraz");
        _containerGrid.Columns.Add("Status", "Status");
        _containerGrid.Columns["Id"]!.Visible = false;
        _containerGrid.Columns["Name"]!.FillWeight = 30;
        _containerGrid.Columns["Image"]!.FillWeight = 35;
        _containerGrid.Columns["Status"]!.FillWeight = 35;
        _containerGrid.SelectionChanged += async (s, e) => await OnContainerSelected();
        Controls.Add(_containerGrid);

        // === Action Buttons Panel ===
        var panelActions = new FlowLayoutPanel
        {
            Location = new Point(720, 12),
            Size = new Size(300, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(5)
        };

        _btnStart = CreateButton("▶ Start", Color.FromArgb(46, 139, 87));
        _btnStop = CreateButton("■ Stop", Color.FromArgb(178, 34, 34));
        _btnRestart = CreateButton("↻ Restart", Color.FromArgb(70, 130, 180));
        _btnDelete = CreateButton("✕ Usuń", Color.FromArgb(139, 0, 0));
        _btnInstallMySQL = CreateButton("🐬 Zainstaluj MySQL", Color.FromArgb(0, 120, 170));
        _btnInstallPostgres = CreateButton("🐘 Zainstaluj PostgreSQL", Color.FromArgb(50, 100, 150));

        _btnStart.Click += async (s, e) => await ContainerAction("start");
        _btnStop.Click += async (s, e) => await ContainerAction("stop");
        _btnRestart.Click += async (s, e) => await ContainerAction("restart");
        _btnDelete.Click += async (s, e) => await ContainerAction("delete");
        _btnInstallMySQL.Click += async (s, e) => await InstallDatabase("mysql");
        _btnInstallPostgres.Click += async (s, e) => await InstallDatabase("postgres");

        panelActions.Controls.AddRange(new Control[]
        {
            _btnStart, _btnStop, _btnRestart, _btnDelete,
            new Label { Height = 10 },
            _btnInstallMySQL, _btnInstallPostgres
        });
        Controls.Add(panelActions);

        // === Stats Labels ===
        _lblStats = new Label
        {
            Text = "Statystyki zasobów:",
            Location = new Point(12, 320),
            Size = new Size(200, 20),
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
            Anchor = AnchorStyles.Left
        };
        Controls.Add(_lblStats);

        _lblCpu = new Label
        {
            Text = "CPU: —",
            Location = new Point(12, 342),
            Size = new Size(350, 20),
            Anchor = AnchorStyles.Left
        };
        Controls.Add(_lblCpu);

        _lblRam = new Label
        {
            Text = "RAM: —",
            Location = new Point(370, 342),
            Size = new Size(350, 20),
            Anchor = AnchorStyles.Left
        };
        Controls.Add(_lblRam);

        // === Logs TextBox ===
        var lblLogs = new Label
        {
            Text = "Logi kontenera (ostatnie 20 linii):",
            Location = new Point(12, 370),
            Size = new Size(300, 20),
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
            Anchor = AnchorStyles.Left
        };
        Controls.Add(lblLogs);

        _txtLogs = new TextBox
        {
            Location = new Point(12, 392),
            Size = new Size(1010, 280),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220)
        };
        Controls.Add(_txtLogs);
    }

    private Button CreateButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            Size = new Size(270, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 2)
        };
    }

    // =============================================
    // Task 1: Live Status Monitoring (Timer + color)
    // =============================================
    private async Task RefreshContainerList()
    {
        try
        {
            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });

            string? selectedId = null;
            if (_containerGrid.CurrentRow != null)
                selectedId = _containerGrid.CurrentRow.Cells["Id"].Value?.ToString();

            _containerGrid.Rows.Clear();

            foreach (var c in containers)
            {
                var name = c.Names.FirstOrDefault()?.TrimStart('/') ?? "—";
                int rowIdx = _containerGrid.Rows.Add(c.ID, name, c.Image, c.Status);
                var row = _containerGrid.Rows[rowIdx];

                // Color-coded rows: green = running, red = exited
                if (c.State == "running")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(220, 255, 220);
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 220, 220);
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
            }

            // Restore selection
            if (selectedId != null)
            {
                foreach (DataGridViewRow row in _containerGrid.Rows)
                {
                    if (row.Cells["Id"].Value?.ToString() == selectedId)
                    {
                        row.Selected = true;
                        _containerGrid.CurrentCell = row.Cells["Name"];
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _txtLogs.Text = $"Błąd połączenia z Docker: {ex.Message}\r\n\r\nUpewnij się, że Docker Desktop jest uruchomiony.";
        }
    }

    // =============================================
    // Container Actions: Start, Stop, Restart, Delete
    // =============================================
    private async Task ContainerAction(string action)
    {
        var id = GetSelectedContainerId();
        if (id == null)
        {
            MessageBox.Show("Wybierz kontener z listy.", "Brak zaznaczenia",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            switch (action)
            {
                case "start":
                    await _client.Containers.StartContainerAsync(id, new ContainerStartParameters());
                    break;
                case "stop":
                    await _client.Containers.StopContainerAsync(id, new ContainerStopParameters());
                    break;
                case "restart":
                    await _client.Containers.StopContainerAsync(id, new ContainerStopParameters());
                    await _client.Containers.StartContainerAsync(id, new ContainerStartParameters());
                    break;
                case "delete":
                    var confirm = MessageBox.Show("Czy na pewno chcesz usunąć ten kontener?",
                        "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm == DialogResult.Yes)
                    {
                        // Stop first if running
                        try { await _client.Containers.StopContainerAsync(id, new ContainerStopParameters()); }
                        catch { /* already stopped */ }
                        await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true });
                    }
                    break;
            }

            await RefreshContainerList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd: {ex.Message}", "Błąd operacji",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // =============================================
    // Task 2: Container Logs (last 20 lines)
    // =============================================
    private async Task LoadContainerLogs(string containerId)
    {
        try
        {
            var logParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = "20"
            };

            var muxStream = await _client.Containers.GetContainerLogsAsync(
                containerId, false, logParams);

            // Read from MultiplexedStream
            var buffer = new byte[65536];
            using var ms = new MemoryStream();
            MultiplexedStream.ReadResult readResult;
            do
            {
                readResult = await muxStream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);
                if (readResult.Count > 0)
                    ms.Write(buffer, 0, readResult.Count);
            } while (!readResult.EOF);

            var rawLog = Encoding.UTF8.GetString(ms.ToArray());

            _txtLogs.Text = string.IsNullOrWhiteSpace(rawLog)
                ? "(brak logów)"
                : rawLog.TrimEnd();
        }
        catch
        {
            _txtLogs.Text = "(nie można pobrać logów — kontener może być zatrzymany)";
        }
    }

    // =============================================
    // Task 3: Resource Statistics (CPU + RAM)
    // =============================================
    private async Task LoadContainerStats(string containerId)
    {
        try
        {
            ContainerStatsResponse? statsResponse = null;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var progress = new Progress<ContainerStatsResponse>(stats =>
            {
                statsResponse = stats;
            });

            // Stream=false gives a single snapshot then completes
            await _client.Containers.GetContainerStatsAsync(
                containerId,
                new ContainerStatsParameters { Stream = false },
                progress,
                cts.Token);

            if (statsResponse != null)
            {
                // CPU %
                double cpuPercent = 0.0;
                var cpuDelta = statsResponse.CPUStats.CPUUsage.TotalUsage
                             - statsResponse.PreCPUStats.CPUUsage.TotalUsage;
                var systemDelta = statsResponse.CPUStats.SystemUsage
                                - statsResponse.PreCPUStats.SystemUsage;
                var cpuCount = statsResponse.CPUStats.OnlineCPUs;
                if (cpuCount == 0) cpuCount = (uint)(statsResponse.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1);

                if (systemDelta > 0 && cpuDelta > 0)
                {
                    cpuPercent = ((double)cpuDelta / systemDelta) * cpuCount * 100.0;
                }

                // RAM
                var memUsage = statsResponse.MemoryStats.Usage;
                var memLimit = statsResponse.MemoryStats.Limit;
                var cache = statsResponse.MemoryStats.Stats?.TryGetValue("cache", out var cacheVal) == true
                    ? cacheVal : 0;
                var actualMem = memUsage - (ulong)cache;

                _lblCpu.Text = $"CPU: {cpuPercent:F2}%";
                _lblRam.Text = $"RAM: {FormatBytes(actualMem)} / {FormatBytes(memLimit)}";
            }
        }
        catch
        {
            _lblCpu.Text = "CPU: —";
            _lblRam.Text = "RAM: —";
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    // =============================================
    // Selection handler — loads logs + stats
    // =============================================
    private async Task OnContainerSelected()
    {
        var id = GetSelectedContainerId();
        if (id == null)
        {
            _txtLogs.Text = "";
            _lblCpu.Text = "CPU: —";
            _lblRam.Text = "RAM: —";
            return;
        }

        await LoadContainerLogs(id);
        await LoadContainerStats(id);
    }

    // =============================================
    // Task 4 & 5: Database Installer with Volume + Port Check
    // =============================================
    private async Task InstallDatabase(string dbType)
    {
        bool isMySQL = dbType == "mysql";
        string image = isMySQL ? "mysql:latest" : "postgres:latest";
        string containerName = isMySQL ? "mysql-docker-manager" : "postgres-docker-manager";
        int port = isMySQL ? 3306 : 5432;
        string dataPath = isMySQL ? "/var/lib/mysql" : "/var/lib/postgresql/data";

        // Task 5: Port availability check
        if (IsPortInUse(port))
        {
            MessageBox.Show(
                $"Port {port} jest już zajęty!\n\nZamknij aplikację korzystającą z tego portu lub użyj innego.",
                "Port zajęty", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Task 4: Volume management — let user pick a local folder
        string? volumePath = null;
        var volumeQuestion = MessageBox.Show(
            "Czy chcesz zamontować lokalny folder dla trwałości danych?",
            "Wolumen danych", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (volumeQuestion == DialogResult.Yes)
        {
            using var folderDialog = new FolderBrowserDialog
            {
                Description = $"Wybierz folder do montowania jako {dataPath}",
                UseDescriptionForTitle = true
            };

            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                volumePath = folderDialog.SelectedPath;
            }
            else
            {
                return; // User cancelled
            }
        }

        // Disable buttons during install
        _btnInstallMySQL.Enabled = false;
        _btnInstallPostgres.Enabled = false;
        _txtLogs.Text = $"Pobieranie obrazu {image}...\r\nTo może potrwać kilka minut.";

        try
        {
            // Pull image
            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = image.Split(':')[0], Tag = "latest" },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    BeginInvoke(() =>
                    {
                        if (!string.IsNullOrEmpty(msg.Status))
                            _txtLogs.Text = $"Pobieranie: {msg.Status} {msg.ProgressMessage}";
                    });
                }));

            // Build environment variables
            var envVars = isMySQL
                ? new List<string> { "MYSQL_ROOT_PASSWORD=rootpassword", "MYSQL_DATABASE=testdb" }
                : new List<string> { "POSTGRES_PASSWORD=rootpassword", "POSTGRES_DB=testdb" };

            // Build port bindings
            var portBindings = new Dictionary<string, IList<PortBinding>>
            {
                {
                    $"{port}/tcp",
                    new List<PortBinding> { new() { HostPort = port.ToString() } }
                }
            };

            // Build volume binds
            var binds = new List<string>();
            if (volumePath != null)
            {
                binds.Add($"{volumePath}:{dataPath}");
            }

            // Create container
            var createResponse = await _client.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = image,
                    Name = containerName,
                    Env = envVars,
                    ExposedPorts = new Dictionary<string, EmptyStruct>
                    {
                        { $"{port}/tcp", default }
                    },
                    HostConfig = new HostConfig
                    {
                        PortBindings = portBindings,
                        Binds = binds.Count > 0 ? binds : null,
                        RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped }
                    }
                });

            // Start container
            await _client.Containers.StartContainerAsync(
                createResponse.ID, new ContainerStartParameters());

            await RefreshContainerList();

            var volumeInfo = volumePath != null ? $"\nDane montowane z: {volumePath}" : "";
            MessageBox.Show(
                $"{(isMySQL ? "MySQL" : "PostgreSQL")} zainstalowany i uruchomiony!\n\n" +
                $"Port: {port}\nHasło root: rootpassword\nBaza: testdb{volumeInfo}",
                "Sukces", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Błąd instalacji: {ex.Message}", "Błąd",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnInstallMySQL.Enabled = true;
            _btnInstallPostgres.Enabled = true;
        }
    }

    // =============================================
    // Task 5: Port Availability Check
    // =============================================
    private static bool IsPortInUse(int port)
    {
        var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
        var listeners = ipProperties.GetActiveTcpListeners();
        return listeners.Any(ep => ep.Port == port);
    }

    // =============================================
    // Helpers
    // =============================================
    private string? GetSelectedContainerId()
    {
        if (_containerGrid.CurrentRow == null) return null;
        return _containerGrid.CurrentRow.Cells["Id"].Value?.ToString();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer.Stop();
        _client.Dispose();
        base.OnFormClosing(e);
    }
}
