namespace OnePlusWebcam;

internal sealed class DriverSetupForm : Form
{
    public static int Run()
    {
        using var form = new DriverSetupForm();
        Application.Run(form);
        return form._exitCode;
    }

    private readonly TextBox _log = new();
    private readonly FileLogger _fileLog;
    private int _exitCode = 1;
    private bool _started;

    private DriverSetupForm()
    {
        Directory.CreateDirectory(ConfigStore.DefaultDirectory);
        _fileLog = new FileLogger(Path.Combine(ConfigStore.DefaultDirectory, "driver-setup.log"));

        Text = "OnePlus Webcam driver";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 360);
        TopMost = true;
        ShowInTaskbar = true;

        var caption = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(16, 12, 528, 40),
            Text = "Installing the virtual webcam driver. Accept any Windows security prompt, then wait until this window closes.",
        };

        _log.Bounds = new Rectangle(16, 56, 528, 288);
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Font = new Font("Consolas", 9F);
        _log.WordWrap = false;

        Controls.Add(caption);
        Controls.Add(_log);
        Shown += OnShown;
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        try
        {
            _exitCode = await DriverRegistration.RunAsync(Append, CancellationToken.None).ConfigureAwait(true);
            if (_exitCode == 0)
            {
                MessageBox.Show(
                    this,
                    "The OnePlus Webcam driver is installed. Restart Zoom, Teams, or Discord and choose the camera named OnePlus Webcam.",
                    "OnePlus Webcam",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    this,
                    "The virtual webcam driver did not start. Re-run OnePlusWebcam-Setup.exe and accept the administrator prompt, or right-click OnePlusWebcam.exe and choose Run as administrator.",
                    "OnePlus Webcam",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Append(ex.ToString());
            _exitCode = 1;
            MessageBox.Show(this, ex.Message, "OnePlus Webcam", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Close();
        }
    }

    private void Append(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => Append(message));
            return;
        }

        _fileLog.Write(message);
        _log.AppendText(message + Environment.NewLine);
    }
}
