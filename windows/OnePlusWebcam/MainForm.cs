namespace OnePlusWebcam;

internal sealed class MainForm : Form
{
    private readonly WebcamPipeline _pipeline;
    private readonly FileLogger _log;
    private readonly AppConfig _config;

    private readonly Label lblStatus = new();
    private readonly ComboBox cmbDevice = new();
    private readonly ComboBox cmbCamera = new();
    private readonly ComboBox cmbSize = new();
    private readonly NumericUpDown nudFps = new();
    private readonly NumericUpDown nudZoom = new();
    private readonly CheckBox chkPreview = new();
    private readonly CheckBox chkStartWithWindows = new();
    private readonly Button btnRefresh = new();
    private readonly Button btnStartStop = new();
    private readonly Label lblError = new();
    private readonly Label lblDevice = new();
    private readonly Label lblCamera = new();
    private readonly System.Windows.Forms.Timer _timer = new();

    private List<PhoneDevice> _phones = [];
    private List<CameraInfo> _cameras = [];
    private bool _busy;
    private bool _syncing;

    public MainForm(WebcamPipeline pipeline, FileLogger log, AppConfig config)
    {
        _pipeline = pipeline;
        _log = log;
        _config = config;
        BuildUi();
        Load += async (_, _) => await RefreshAllAsync().ConfigureAwait(true);
        _timer.Tick += async (_, _) => await OnTimerAsync().ConfigureAwait(true);
        VisibleChanged += (_, _) => _timer.Interval = Visible ? 3000 : 10000;
        _timer.Interval = 3000;
        _timer.Start();
    }

    private bool _allowClose;

    public void RequestExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _timer.Stop();
        PersistConfig();
        base.OnFormClosing(e);
    }

    public async Task StartFromTrayAsync()
    {
        Show();
        Activate();
        await StartOrStopAsync(forceStart: true).ConfigureAwait(true);
    }

    public async Task StopFromTrayAsync()
    {
        if (_pipeline.IsRunning)
        {
            _pipeline.Stop();
            UpdateRunningUi();
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private void BuildUi()
    {
        Text = "OnePlus Webcam";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 560);
        Font = new Font("Segoe UI", 9F);

        lblStatus.SetBounds(16, 16, 388, 40);
        lblStatus.AutoSize = false;

        lblDevice.SetBounds(16, 64, 388, 20);
        lblDevice.Text = "Phone";
        cmbDevice.SetBounds(16, 84, 388, 28);
        cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDevice.DisplayMember = "Model";
        cmbDevice.ValueMember = "Serial";
        cmbDevice.SelectedIndexChanged += async (_, _) =>
        {
            if (_syncing)
            {
                return;
            }

            await RefreshCamerasAsync().ConfigureAwait(true);
        };

        lblCamera.SetBounds(16, 124, 388, 20);
        lblCamera.Text = "Lens";
        cmbCamera.SetBounds(16, 144, 388, 28);
        cmbCamera.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCamera.SelectedIndexChanged += (_, _) => ApplyZoomRange();

        var lblSize = new Label { Text = "Size", Bounds = new Rectangle(16, 184, 388, 20) };
        cmbSize.SetBounds(16, 204, 388, 28);
        cmbSize.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSize.Items.AddRange(PipelineCommands.SizeChoices.Cast<object>().ToArray());
        cmbSize.SelectedItem = _config.Size;
        if (cmbSize.SelectedIndex < 0)
        {
            cmbSize.SelectedItem = "1920x1080";
        }

        var lblFps = new Label { Text = "FPS", Bounds = new Rectangle(16, 244, 180, 20) };
        nudFps.SetBounds(16, 264, 180, 28);
        nudFps.Minimum = 10;
        nudFps.Maximum = 60;
        nudFps.Increment = 5;
        nudFps.Value = Math.Clamp(_config.Fps, 10, 60);

        var lblZoom = new Label { Text = "Zoom", Bounds = new Rectangle(224, 244, 180, 20) };
        nudZoom.SetBounds(224, 264, 180, 28);
        nudZoom.Minimum = 1;
        nudZoom.Maximum = 20;
        nudZoom.Increment = 1;
        nudZoom.DecimalPlaces = 0;
        nudZoom.Value = (decimal)Math.Clamp(_config.Zoom, 1, 20);

        chkPreview.SetBounds(16, 308, 388, 24);
        chkPreview.Text = "Show preview window";
        chkPreview.Checked = _config.Preview;

        chkStartWithWindows.SetBounds(16, 336, 388, 24);
        chkStartWithWindows.Text = "Start with Windows";
        chkStartWithWindows.Checked = Autostart.IsEnabled();
        chkStartWithWindows.CheckedChanged += (_, _) =>
        {
            Autostart.SetEnabled(chkStartWithWindows.Checked);
            _config.StartWithWindows = chkStartWithWindows.Checked;
        };

        btnRefresh.SetBounds(16, 376, 180, 36);
        btnRefresh.Text = "Refresh";
        btnRefresh.Click += async (_, _) => await RefreshAllAsync().ConfigureAwait(true);

        btnStartStop.SetBounds(224, 376, 180, 36);
        btnStartStop.Text = "Start webcam";
        btnStartStop.Click += async (_, _) => await StartOrStopAsync(forceStart: false).ConfigureAwait(true);

        lblError.SetBounds(16, 428, 388, 112);
        lblError.ForeColor = Color.Firebrick;
        lblError.AutoSize = false;

        Controls.AddRange(
        [
            lblStatus, lblDevice, cmbDevice, lblCamera, cmbCamera, lblSize, cmbSize,
            lblFps, nudFps, lblZoom, nudZoom, chkPreview, chkStartWithWindows,
            btnRefresh, btnStartStop, lblError,
        ]);
    }

    private PhoneDevice? SelectedPhone()
    {
        if (cmbDevice.SelectedItem is PhoneDevice phone)
        {
            return phone;
        }

        return _phones.Count == 1 ? _phones[0] : null;
    }

    private CameraInfo? SelectedCamera()
    {
        if (cmbCamera.SelectedItem is CameraItem item)
        {
            return item.Info;
        }

        return _cameras.FirstOrDefault();
    }

    private async Task OnTimerAsync()
    {
        if (_busy)
        {
            return;
        }

        await RefreshPhonesAsync().ConfigureAwait(true);
        if (Visible && !_pipeline.IsRunning)
        {
            await RefreshCamerasAsync().ConfigureAwait(true);
        }

        UpdateRunningUi();
    }

    private async Task RefreshAllAsync()
    {
        lblError.Text = "";
        var vcam = await _pipeline.EnsureVirtualCameraAsync(CancellationToken.None).ConfigureAwait(true);
        if (vcam is not null)
        {
            lblError.Text = vcam;
        }

        await RefreshPhonesAsync().ConfigureAwait(true);
        await RefreshCamerasAsync().ConfigureAwait(true);
        UpdateRunningUi();
    }

    private async Task RefreshPhonesAsync()
    {
        try
        {
            var phones = (await _pipeline.ListPhonesAsync(CancellationToken.None).ConfigureAwait(true)).ToList();
            _phones = phones;
            _syncing = true;
            var previous = cmbDevice.SelectedItem is PhoneDevice p ? p.Serial : _config.Serial;
            cmbDevice.DataSource = null;
            cmbDevice.DisplayMember = "Model";
            cmbDevice.ValueMember = "Serial";
            cmbDevice.DataSource = phones;
            cmbDevice.Visible = phones.Count > 1;
            lblDevice.Visible = phones.Count > 1;

            if (previous is not null)
            {
                var match = phones.Find(d => d.Serial == previous);
                if (match is not null)
                {
                    cmbDevice.SelectedItem = match;
                }
            }

            _syncing = false;
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
            _log.Write(ex.ToString());
        }
    }

    private async Task RefreshCamerasAsync()
    {
        var phone = SelectedPhone();
        if (phone is null || phone.Status != DeviceAdbStatus.Device)
        {
            _cameras = [];
            cmbCamera.DataSource = null;
            UpdateRunningUi();
            return;
        }

        try
        {
            var cameras = (await _pipeline.ListCamerasAsync(phone.Serial, CancellationToken.None).ConfigureAwait(true)).ToList();
            _cameras = cameras;
            var items = cameras.Select(c => new CameraItem(c)).ToList();
            var keepId = cmbCamera.SelectedItem is CameraItem cur ? cur.Info.Id : _config.CameraId;
            cmbCamera.DataSource = null;
            cmbCamera.DisplayMember = "Label";
            cmbCamera.DataSource = items;
            var selected = items.Find(i => i.Info.Id == keepId);
            if (selected is not null)
            {
                cmbCamera.SelectedItem = selected;
            }

            ApplyZoomRange();
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
            _log.Write(ex.ToString());
        }
    }

    private void ApplyZoomRange()
    {
        var cam = SelectedCamera();
        var max = cam?.ZoomMax > 0 ? cam.ZoomMax : 20;
        nudZoom.Maximum = (decimal)max;
        nudZoom.DecimalPlaces = max % 1 == 0 ? 0 : 1;
        if (nudZoom.Value > nudZoom.Maximum)
        {
            nudZoom.Value = nudZoom.Maximum;
        }
    }

    private async Task StartOrStopAsync(bool forceStart)
    {
        if (_busy)
        {
            return;
        }

        if (_pipeline.IsRunning && !forceStart)
        {
            _pipeline.Stop();
            UpdateRunningUi();
            return;
        }

        var phone = SelectedPhone();
        var error = PipelineCommands.ValidateReadyToStart(phone);
        if (error is not null)
        {
            lblError.Text = error;
            return;
        }

        var camera = SelectedCamera();
        var cameraId = camera?.Id ?? _config.CameraId;
        var size = cmbSize.SelectedItem as string ?? "1920x1080";
        var fps = (int)nudFps.Value;
        var zoom = (double)nudZoom.Value;
        _busy = true;
        btnStartStop.Enabled = false;
        lblError.Text = "";
        try
        {
            await _pipeline.StartAsync(phone!, cameraId, size, fps, zoom, chkPreview.Checked, CancellationToken.None)
                .ConfigureAwait(true);
            _config.Serial = phone!.Serial;
            _config.CameraId = cameraId;
            _config.Size = size;
            _config.Fps = fps;
            _config.Zoom = zoom;
            _config.Preview = chkPreview.Checked;
            PersistConfig();
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
            _log.Write(ex.ToString());
            _pipeline.Stop();
        }
        finally
        {
            _busy = false;
            UpdateRunningUi();
        }
    }

    private void UpdateRunningUi()
    {
        var phone = SelectedPhone();
        var camera = SelectedCamera();
        var size = cmbSize.SelectedItem as string ?? _config.Size;
        var fps = (int)nudFps.Value;
        var running = _pipeline.IsRunning;
        lblStatus.Text = PipelineCommands.StatusLabel(phone, running, camera, size, fps);
        btnStartStop.Text = running ? "Stop webcam" : "Start webcam";
        var ready = phone is { Status: DeviceAdbStatus.Device } && _cameras.Count > 0;
        btnStartStop.Enabled = !_busy && (running || ready);
        if (_cameras.Count == 0 && !running && phone is { Status: DeviceAdbStatus.Device })
        {
            btnStartStop.Enabled = false;
        }
    }

    private void PersistConfig()
    {
        try
        {
            ConfigStore.Save(_config);
        }
        catch (Exception ex)
        {
            _log.Write("config save: " + ex.Message);
        }
    }

    private sealed class CameraItem(CameraInfo info)
    {
        public CameraInfo Info { get; } = info;
        public string Label => $"Camera {info.Id} ({info.Facing})";
    }
}
