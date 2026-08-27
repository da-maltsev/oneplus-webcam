namespace OnePlusWebcam;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly MainForm _form;
    private readonly WebcamPipeline _pipeline;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly ToolStripMenuItem _autostartItem;

    public TrayApplicationContext(MainForm form, WebcamPipeline pipeline)
    {
        _form = form;
        _pipeline = pipeline;
        _startItem = new ToolStripMenuItem("Start", null, async (_, _) => await form.StartFromTrayAsync().ConfigureAwait(true));
        _stopItem = new ToolStripMenuItem("Stop", null, async (_, _) => await form.StopFromTrayAsync().ConfigureAwait(true));
        _autostartItem = new ToolStripMenuItem("Start with Windows", null, OnAutostart)
        {
            Checked = Autostart.IsEnabled(),
            CheckOnClick = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowForm());
        menu.Items.Add(_startItem);
        menu.Items.Add(_stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autostartItem);
        menu.Items.Add("Exit", null, (_, _) => Exit());
        menu.Opening += (_, _) =>
        {
            var running = _pipeline.IsRunning;
            _startItem.Enabled = !running;
            _stopItem.Enabled = running;
            _autostartItem.Checked = Autostart.IsEnabled();
        };

        _tray = new NotifyIcon
        {
            Text = "OnePlus Webcam",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowForm();
            }
        };

        ShowForm();
    }

    private void OnAutostart(object? sender, EventArgs e)
    {
        Autostart.SetEnabled(_autostartItem.Checked);
    }

    private void ShowForm()
    {
        _form.Show();
        _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    private void Exit()
    {
        _pipeline.Stop();
        _tray.Visible = false;
        _form.RequestExit();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _form.Dispose();
            _pipeline.Dispose();
        }

        base.Dispose(disposing);
    }
}
