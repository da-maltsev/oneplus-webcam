# oneplus-webcam

Use a OnePlus phone as a USB webcam on Omarchy (Arch + Hyprland).

Two parts ship in this repo:

1. **`bin/oneplus-cam`** — a CLI that streams the phone's camera into a
   v4l2loopback device (`/dev/video60`) via `scrcpy`, video only, no audio.
2. **Omarchy shell plugin** (`manifest.json` + `Panel.qml`) — a bar widget in
   the top-right corner that shows a live status dot and opens a panel for
   picking the lens, zoom, size, fps and starting/stopping the stream.

## Requirements

- Omarchy (or Arch with `v4l2loopback`, `scrcpy`, `android-tools` installed)
- A OnePlus phone with **Developer options → USB debugging** enabled

## Install / restore

```bash
./install.sh
```

The script (idempotent, run as your user, `sudo` where needed):

1. Installs `scrcpy`, `android-tools`, `v4l2loopback-dkms`
2. Writes `/etc/modprobe.d/v4l2loopback-camera.conf` (two loopback devices:
   `/dev/video50` = the built-in IPU7 camera relay, `/dev/video60` = the phone)
3. Installs `oneplus-cam` to `~/.local/bin`
4. Installs the plugin to `~/.config/omarchy/plugins/oneplus-cam`
5. Enables the plugin (adds the widget to the bar)

After a fresh OS install this fully restores the setup. Then:

1. Plug in the phone via USB.
2. Accept the **"Allow USB debugging?"** prompt on the phone, tick *Always allow*.
3. Open the **OnePlus Webcam** widget in the top-right bar → pick a lens → **Start**.
   Or from the CLI: `oneplus-cam start`

## CLI usage

```
oneplus-cam start [--facing back|front] [--camera-id N] [--zoom N] [--size WxH] [--fps N] [--preview]
oneplus-cam stop
oneplus-cam status
oneplus-cam state          # machine-readable JSON
oneplus-cam cams [--json]  # list available cameras
```

`start` runs **headless** by default (no window). Pass `--preview` to also open a
camera preview window (still feeding the v4l2 webcam).

## Camera ids

scrcpy exposes the phone's cameras by id (see `oneplus-cam cams`):

| id | facing | notes |
|----|--------|-------|
| 0  | back   | main lens, zoom range 1–20 |
| 1  | front  | selfie, zoom range 1–10 |
| 2  | back   | other rear lens |
| 3  | back   | other rear lens |

## Widget

- **Status dot**: green when streaming; the icon grays out (disabled) when no
  phone is connected.
- **Panel** (click the camera icon): lens dropdown, zoom, size, fps,
  a "With preview" toggle, Start/Stop button, and live status. Keyboard:
  `s` start/stop, `r` refresh, `Esc` close.

## Windows

Any **Android 12+** phone (not just OnePlus) can be used as a webcam on Windows 10/11. You do not need a terminal.

1. Download **OnePlusWebcam-Setup.exe** from [Releases](https://github.com/da-maltsev/oneplus-webcam/releases).
2. Double-click the installer and accept the one-time administrator prompt (virtual-camera driver).
3. On the phone, enable **Developer options → USB debugging**, plug in USB, and tap **Always allow**.
4. Open **OnePlus Webcam** from the Start Menu. Pick a lens and **Start webcam**.
5. In Zoom, Teams, or Discord, choose the camera named **OnePlus Webcam**.

Closing the window hides the app in the system tray (right-click the tray icon → Exit to quit). Optional: **Start with Windows**.

Chrome / Google Meet sometimes cannot open DirectShow virtual cameras. If the device does not appear there, use [OBS Studio](https://obsproject.com/) **Start Virtual Camera** as a fallback.

The Windows app lives under `windows/`. GitHub Actions (tag `v*` or manual **workflow_dispatch**) builds `OnePlusWebcam-Setup.exe`.

## Files

- `manifest.json`, `Panel.qml` — the Omarchy shell plugin
- `bin/oneplus-cam` — the CLI helper
- `install.sh` — full restore script
- `windows/` — Windows tray app and Inno Setup installer

## License

MIT
