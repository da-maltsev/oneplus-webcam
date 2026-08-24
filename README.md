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
oneplus-cam start [--facing back|front] [--camera-id N] [--zoom N] [--size WxH] [--fps N]
oneplus-cam stop
oneplus-cam status
oneplus-cam state          # machine-readable JSON
oneplus-cam cams [--json]  # list available cameras
```

## Camera ids

scrcpy exposes the phone's cameras by id (see `oneplus-cam cams`):

| id | facing | notes |
|----|--------|-------|
| 0  | back   | main lens, zoom range 1–20 |
| 1  | front  | selfie, zoom range 1–10 |
| 2  | back   | other rear lens |
| 3  | back   | other rear lens |

## Widget

- **Status dot**: green = streaming, amber = phone connected, red = no phone.
- **Panel** (click the camera icon): lens dropdown, zoom, size, fps,
  Start/Stop button, and live status. Keyboard: `s` start/stop, `r` refresh,
  `Esc` close.

## Files

- `manifest.json`, `Panel.qml` — the Omarchy shell plugin
- `bin/oneplus-cam` — the CLI helper
- `install.sh` — full restore script

## License

MIT
