#!/usr/bin/env bash
set -euo pipefail

# Install / restore the OnePlus-as-webcam setup on Omarchy (Arch + Hyprland).
#
#   1. system packages        scrcpy, android-tools, v4l2loopback-dkms
#   2. v4l2loopback config    second device /dev/video60 ("OnePlus 13 Webcam")
#   3. CLI helper             oneplus-cam -> ~/.local/bin
#   4. Omarchy shell plugin   -> ~/.config/omarchy/plugins/oneplus-cam
#   5. enable the plugin      adds the "OnePlus Webcam" widget to the bar
#
# Idempotent: safe to re-run.

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_DIR="${BIN_DIR:-$HOME/.local/bin}"
PLUGIN_DIR="${PLUGIN_DIR:-$HOME/.config/omarchy/plugins/oneplus-cam}"

echo "==> [1/5] System packages (scrcpy, android-tools, v4l2loopback-dkms)"
if command -v pacman >/dev/null 2>&1; then
    sudo pacman -S --needed --noconfirm scrcpy android-tools v4l2loopback-dkms
else
    echo "WARNING: no pacman found. Install scrcpy, android-tools and"
    echo "         v4l2loopback-dkms manually, then re-run this script."
fi

echo "==> [2/5] v4l2loopback config (video50 = IPU7 laptop cam, video60 = OnePlus)"
sudo tee /etc/modprobe.d/v4l2loopback-camera.conf >/dev/null <<'EOF'
options v4l2loopback devices=2 video_nr=50,60 card_label="Hardware ISP Camera,OnePlus 13 Webcam" exclusive_caps=1,1 max_buffers=16
EOF

if lsmod | grep -q '^v4l2loopback'; then
    echo "    Reloading v4l2loopback to apply the two-device config..."
    sudo systemctl stop v4l2-relayd@ipu7.service 2>/dev/null || true
    sudo modprobe -r v4l2loopback
    sudo modprobe v4l2loopback
    sudo systemctl start v4l2-relayd@ipu7.service 2>/dev/null || true
    sleep 2
fi

if [[ ! -e /dev/video60 ]]; then
    echo "ERROR: /dev/video60 was not created. Check the v4l2loopback module."
    exit 1
fi

echo "==> [3/5] CLI helper -> $BIN_DIR/oneplus-cam"
install -Dm755 "$REPO_DIR/bin/oneplus-cam" "$BIN_DIR/oneplus-cam"

echo "==> [4/5] Shell plugin -> $PLUGIN_DIR"
mkdir -p "$PLUGIN_DIR"
install -m644 "$REPO_DIR/manifest.json" "$PLUGIN_DIR/manifest.json"
install -m644 "$REPO_DIR/Panel.qml" "$PLUGIN_DIR/Panel.qml"

echo "==> [5/5] Enable plugin in the Omarchy shell"
omarchy-shell shell rescanPlugins 2>/dev/null || true
omarchy plugin enable oneplus-cam 2>/dev/null || true

cat <<EOF

Done.

Next steps:
  1. Plug in the OnePlus phone via USB.
  2. On the phone accept the "Allow USB debugging?" prompt (tick "Always allow").
  3. Use the "OnePlus Webcam" widget (top-right bar) to pick a lens and Start.
     Or from the CLI:  oneplus-cam start

Config lives in ~/.config/oneplus-cam.conf
Log file for scrcpy:   /tmp/oneplus-cam.log
EOF
