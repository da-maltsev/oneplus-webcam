using System.Diagnostics;

namespace OnePlusWebcam;

internal sealed class WebcamPipeline : IDisposable
{
    private readonly ToolPaths _tools;
    private readonly FileLogger _log;
    private readonly object _gate = new();
    private ProcessJob? _job;
    private Process? _ffmpeg;
    private CancellationTokenSource? _copyCts;
    private string? _activeSerial;
    private bool _running;

    public WebcamPipeline(ToolPaths tools, FileLogger log)
    {
        _tools = tools;
        _log = log;
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running && _ffmpeg is { HasExited: false };
            }
        }
    }

    public async Task StartAsync(
        PhoneDevice phone,
        string cameraId,
        string size,
        int fps,
        double zoom,
        bool preview,
        CancellationToken cancellationToken)
    {
        var missing = _tools.MissingCaptureToolsMessage();
        if (missing is not null)
        {
            throw new InvalidOperationException(missing);
        }

        var vcam = await EnsureVirtualCameraAsync(cancellationToken).ConfigureAwait(false);
        if (vcam is not null)
        {
            throw new InvalidOperationException(vcam);
        }

        var validation = PipelineCommands.ValidateReadyToStart(phone);
        if (validation is not null)
        {
            throw new InvalidOperationException(validation);
        }

        Stop();

        var adb = _tools.Adb;
        var serial = phone.Serial;
        var versionOut = await RunCapturedAsync(PipelineCommands.ScrcpyVersion(_tools.Scrcpy), TimeSpan.FromSeconds(10), cancellationToken)
            .ConfigureAwait(false);
        var version = ScrcpyVersionParser.Parse(versionOut.Output, out var usedFallback);
        if (usedFallback)
        {
            _log.Write("WARNING: could not parse scrcpy --version; using bundled 4.1");
        }

        await RunCapturedAsync(PipelineCommands.AdbPushServer(adb, serial, _tools.ScrcpyServer), TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);
        _ = await RunCapturedAsync(PipelineCommands.AdbForwardRemove(adb, serial), TimeSpan.FromSeconds(5), cancellationToken)
            .ConfigureAwait(false);
        var forward = await RunCapturedAsync(PipelineCommands.AdbForward(adb, serial), TimeSpan.FromSeconds(10), cancellationToken)
            .ConfigureAwait(false);
        if (forward.ExitCode != 0)
        {
            throw new InvalidOperationException("adb forward failed. " + forward.Output.Trim());
        }

        var job = new ProcessJob();
        CancellationTokenSource? copyCts = null;
        try
        {
            var server = Start(PipelineCommands.AdbStartCameraServer(adb, serial, version, cameraId, size, fps, zoom));
            job.Add(server);
            AttachTextLog(server);

            var started = DateTime.UtcNow;
            while ((DateTime.UtcNow - started).TotalMilliseconds < 1500)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_log.Tail(4000).Contains("Device:", StringComparison.Ordinal))
                {
                    break;
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            var ffmpeg = Start(PipelineCommands.FfmpegStream(_tools.Ffmpeg, size, fps));
            job.Add(ffmpeg);
            AttachErrorLog(ffmpeg);

            var akvcam = Start(PipelineCommands.AkVCamStream(_tools.AkVCamManager!, size, fps), redirectInput: true);
            job.Add(akvcam);
            AttachTextLog(akvcam);

            copyCts = new CancellationTokenSource();
            var ffmpegOut = ffmpeg.StandardOutput.BaseStream;
            var akIn = akvcam.StandardInput.BaseStream;
            var copyToken = copyCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await ffmpegOut.CopyToAsync(akIn, copyToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
                {
                    _log.Write("pipe closed: " + ex.Message);
                }
                finally
                {
                    try
                    {
                        akvcam.StandardInput.Close();
                    }
                    catch (Exception ex)
                    {
                        _log.Write("akvcam stdin close: " + ex.Message);
                    }
                }
            }, copyToken);

            if (preview && File.Exists(_tools.Ffplay))
            {
                try
                {
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    var ffplay = Start(PipelineCommands.FfplayPreview(_tools.Ffplay, size, fps));
                    job.Add(ffplay);
                    AttachTextLog(ffplay);
                }
                catch (Exception ex)
                {
                    _log.Write("preview failed (webcam continues): " + ex.Message);
                }
            }

            var exitedEarly = await WaitForExitAsync(ffmpeg, TimeSpan.FromSeconds(3), cancellationToken)
                .ConfigureAwait(false);
            if (exitedEarly && ffmpeg.ExitCode != 0)
            {
                throw new InvalidOperationException("ffmpeg failed to start.\n" + _log.Tail());
            }

            lock (_gate)
            {
                _job = job;
                _ffmpeg = ffmpeg;
                _copyCts = copyCts;
                _activeSerial = serial;
                _running = true;
            }

            copyCts = null;
        }
        catch
        {
            copyCts?.Cancel();
            copyCts?.Dispose();
            job.Dispose();
            throw;
        }
    }

    public void Stop()
    {
        string? serial;
        ProcessJob? job;
        CancellationTokenSource? copyCts;
        lock (_gate)
        {
            serial = _activeSerial;
            job = _job;
            copyCts = _copyCts;
            _job = null;
            _ffmpeg = null;
            _copyCts = null;
            _activeSerial = null;
            _running = false;
        }

        copyCts?.Cancel();
        copyCts?.Dispose();
        job?.Dispose();

        if (!string.IsNullOrEmpty(serial) && File.Exists(_tools.Adb))
        {
            try
            {
                RunCapturedAsync(PipelineCommands.AdbForwardRemove(_tools.Adb, serial), TimeSpan.FromSeconds(5), CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                _log.Write("forward remove: " + ex.Message);
            }

            try
            {
                RunCapturedAsync(PipelineCommands.AdbPkillServer(_tools.Adb, serial), TimeSpan.FromSeconds(5), CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                _log.Write("pkill: " + ex.Message);
            }
        }
    }

    public async Task<IReadOnlyList<PhoneDevice>> ListPhonesAsync(CancellationToken cancellationToken)
    {
        var missing = _tools.MissingCaptureToolsMessage();
        if (missing is not null)
        {
            throw new InvalidOperationException(missing);
        }

        var result = await RunCapturedAsync(PipelineCommands.AdbDevices(_tools.Adb), TimeSpan.FromSeconds(10), cancellationToken)
            .ConfigureAwait(false);
        var parsed = AdbDeviceParser.Parse(result.Output);
        var enriched = new List<PhoneDevice>(parsed.Count);
        foreach (var phone in parsed)
        {
            var model = phone.Model;
            var sdk = phone.Sdk;
            if (phone.Status == DeviceAdbStatus.Device)
            {
                if (model == phone.Serial)
                {
                    model = (await RunCapturedAsync(
                            PipelineCommands.AdbGetprop(_tools.Adb, phone.Serial, "ro.product.model"),
                            TimeSpan.FromSeconds(5),
                            cancellationToken)
                        .ConfigureAwait(false)).Output.Trim();
                    if (string.IsNullOrWhiteSpace(model))
                    {
                        model = phone.Serial;
                    }
                }

                var sdkText = (await RunCapturedAsync(
                        PipelineCommands.AdbGetprop(_tools.Adb, phone.Serial, "ro.build.version.sdk"),
                        TimeSpan.FromSeconds(5),
                        cancellationToken)
                    .ConfigureAwait(false)).Output.Trim();
                _ = int.TryParse(sdkText, out sdk);
            }

            enriched.Add(phone with { Model = model, Sdk = sdk });
        }

        return enriched;
    }

    public async Task<IReadOnlyList<CameraInfo>> ListCamerasAsync(string serial, CancellationToken cancellationToken)
    {
        var missing = _tools.MissingCaptureToolsMessage();
        if (missing is not null)
        {
            throw new InvalidOperationException(missing);
        }

        var result = await RunCapturedAsync(
                PipelineCommands.ScrcpyListCameras(_tools.Scrcpy, serial),
                TimeSpan.FromSeconds(20),
                cancellationToken)
            .ConfigureAwait(false);
        return CameraListParser.Parse(result.Output);
    }

    public async Task<string?> EnsureVirtualCameraAsync(CancellationToken cancellationToken)
    {
        var manager = _tools.AkVCamManager;
        if (string.IsNullOrEmpty(manager) || !File.Exists(manager))
        {
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        try
        {
            var query = await RunCapturedAsync(
                    PipelineCommands.ScQueryAssistant(sc),
                    TimeSpan.FromSeconds(5),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!PipelineCommands.ScQueryReportsRunning(query.Output))
            {
                return PipelineCommands.VirtualCameraDriverHelp;
            }
        }
        catch (TimeoutException ex)
        {
            _log.Write(ex.Message);
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        string? listedOutput = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var listed = await RunCapturedAsync(
                        PipelineCommands.AkVCamDevices(manager),
                        TimeSpan.FromSeconds(8),
                        cancellationToken)
                    .ConfigureAwait(false);
                listedOutput = listed.Output;
                break;
            }
            catch (TimeoutException ex)
            {
                _log.Write(ex.Message);
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }
        }

        if (listedOutput is null)
        {
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        if (listedOutput.Contains(PipelineCommands.VcamDeviceId, StringComparison.OrdinalIgnoreCase)
            || listedOutput.Contains(PipelineCommands.VcamDescription, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(_tools.VcamIni))
        {
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        try
        {
            var load = await RunCapturedAsync(
                    PipelineCommands.AkVCamLoad(manager, _tools.VcamIni),
                    TimeSpan.FromSeconds(8),
                    cancellationToken)
                .ConfigureAwait(false);
            _ = await RunCapturedAsync(
                    PipelineCommands.AkVCamSetPageSize(manager),
                    TimeSpan.FromSeconds(5),
                    cancellationToken)
                .ConfigureAwait(false);
            if (load.ExitCode != 0)
            {
                return PipelineCommands.VirtualCameraDriverHelp;
            }
        }
        catch (TimeoutException ex)
        {
            _log.Write(ex.Message);
            return PipelineCommands.VirtualCameraDriverHelp;
        }

        return null;
    }

    public void Dispose() => Stop();

    private void AttachTextLog(Process process)
    {
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _log.Write(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _log.Write(e.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    private void AttachErrorLog(Process process)
    {
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _log.Write(e.Data);
            }
        };
        process.BeginErrorReadLine();
    }

    private static Process Start(CommandSpec spec, bool redirectInput = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
            WorkingDirectory = spec.WorkingDirectory ?? Path.GetDirectoryName(spec.FileName) ?? "",
        };
        var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start " + spec.FileName);
        }

        return process;
    }

    private async Task<(int ExitCode, string Output)> RunCapturedAsync(
        CommandSpec spec,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = spec.WorkingDirectory ?? Path.GetDirectoryName(spec.FileName) ?? "",
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _log.Write("kill timeout: " + ex.Message);
            }

            try
            {
                await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(1000, CancellationToken.None))
                    .ConfigureAwait(false);
            }
            catch (IOException)
            {
            }

            throw new TimeoutException($"Timed out: {spec.FileName} {spec.Arguments}");
        }

        var text = await stdoutTask.ConfigureAwait(false) + await stderrTask.ConfigureAwait(false);
        _log.Write($"> {spec.FileName} {spec.Arguments} => {process.ExitCode}");
        if (!string.IsNullOrWhiteSpace(text))
        {
            _log.Write(text.TrimEnd());
        }

        return (process.ExitCode, text);
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
