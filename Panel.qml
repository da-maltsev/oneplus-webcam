import QtQuick
import QtQuick.Controls
import Quickshell
import Quickshell.Io
import qs.Commons
import qs.Ui

// OnePlus Webcam — a bar button that opens a panel for starting/stopping the
// phone-as-webcam stream and choosing camera lens, zoom, size and fps.
// All state comes from the `oneplus-cam` CLI (`~/.local/bin/oneplus-cam`):
//   oneplus-cam state      -> JSON with running/cameraId/zoom/size/fps/...
//   oneplus-cam cams --json -> JSON list of {id, facing, maxSize, zoomRange}
//   oneplus-cam start --camera-id N --zoom Z --size WxH --fps F
//   oneplus-cam stop
Panel {
  id: root
  moduleName: "oneplus-cam"
  ipcTarget: "oneplus-cam"

  readonly property string helperPath: {
    var home = Quickshell.env("HOME")
    return (home ? home : "/home/") + "/.local/bin/oneplus-cam"
  }

  readonly property color foreground: bar ? bar.foreground : Color.foreground
  readonly property color urgent: bar ? bar.urgent : Color.urgent
  readonly property color dim: Qt.darker(foreground, 1.55)
  readonly property string fontFamily: bar ? bar.fontFamily : Style.font.family

  // --- live state (parsed from `oneplus-cam state`) ---
  property var state: null
  readonly property bool running: state ? !!state.running : false
  readonly property bool connected: state ? !!state.connected : false
  readonly property bool authorized: state ? !!state.authorized : false
  property var cameras: []
  property bool busy: false
  property string camError: ""

  // --- user selections (synced once from state, then user-controlled) ---
  property string selCamera: "0"
  property int selZoom: 1
  property string selSize: "1920x1080"
  property int selFps: 30
  property bool previewWindow: false
  property bool _synced: false

  // --- stdout/stderr buffers for the three Process slots ---
  property string _stateOut: ""
  property string _camsOut: ""
  property string _actionOut: ""
  property string _actionErr: ""

  readonly property int zoomMax: {
    for (var i = 0; i < cameras.length; i++) {
      if (String(cameras[i].id) === selCamera && cameras[i].zoomRange) {
        var parts = String(cameras[i].zoomRange).split("-")
        if (parts.length === 2) return Math.max(1, Number(parts[1]))
      }
    }
    return 20
  }

  readonly property string cameraLabel: {
    var label = "Camera " + selCamera
    for (var i = 0; i < cameras.length; i++)
      if (String(cameras[i].id) === selCamera)
        label += " (" + cameras[i].facing + ")"
    return label
  }

  readonly property string statusMeta: {
    if (!connected) return "Phone not connected"
    if (!authorized) return "Phone not authorized"
    return running ? "Running" : "Stopped"
  }

  function refreshState() {
    if (stateProcess.running) return
    stateProcess.command = [helperPath, "state"]
    stateProcess.running = true
  }

  function refreshCameras() {
    if (camsProcess.running) return
    camsProcess.command = [helperPath, "cams", "--json"]
    camsProcess.running = true
  }

  function doStart() {
    var args = [helperPath, "start",
      "--camera-id", selCamera,
      "--zoom", String(selZoom),
      "--size", selSize,
      "--fps", String(selFps)]
    if (root.previewWindow) args.push("--preview")
    runAction(args)
  }

  function doStop() {
    runAction([helperPath, "stop"])
  }

  function runAction(args) {
    if (actionProcess.running) return
    busy = true
    actionProcess.command = args
    actionProcess.running = true
  }

  implicitWidth: button.implicitWidth
  implicitHeight: button.implicitHeight

  onOpenedChanged: if (opened) {
    refreshState()
    refreshCameras()
    Qt.callLater(function() { keyCatcher.forceActiveFocus() })
  }

  Timer {
    interval: root.opened ? 3000 : 10000
    running: true
    repeat: true
    onTriggered: root.refreshState()
  }

  Process {
    id: stateProcess
    command: []
    stdout: StdioCollector { waitForEnd: true; onStreamFinished: root._stateOut = text }
    onExited: function(exitCode) {
      if (exitCode !== 0) { root.state = null; return }
      var text = String(root._stateOut || "")
      try {
        root.state = JSON.parse(text)
      } catch (e) {
        root.state = null
      }
      if (!root._synced && root.state) {
        root.selCamera = String(root.state.cameraId !== undefined && root.state.cameraId !== null ? root.state.cameraId : 0)
        root.selZoom = root.state.zoom ? Math.max(1, Number(root.state.zoom)) : 1
        root.selSize = root.state.size ? String(root.state.size) : "1920x1080"
        root.selFps = root.state.fps ? Math.max(10, Number(root.state.fps)) : 30
        root.previewWindow = root.state.preview === 1 || root.state.preview === true
        root._synced = true
      }
    }
  }

  Process {
    id: camsProcess
    command: []
    stdout: StdioCollector { waitForEnd: true; onStreamFinished: root._camsOut = text }
    onExited: function(exitCode) {
      var text = String(root._camsOut || "")
      try {
        var arr = JSON.parse(text)
        root.cameras = Array.isArray(arr) ? arr : []
      } catch (e) {
        root.cameras = []
      }
    }
  }

  Process {
    id: actionProcess
    command: []
    stdout: StdioCollector { waitForEnd: true; onStreamFinished: root._actionOut = text }
    stderr: StdioCollector { waitForEnd: true; onStreamFinished: root._actionErr = text }
    onExited: function(exitCode) {
      root.busy = false
      var err = String(root._actionErr || root._actionOut || "")
      root.camError = (exitCode !== 0) ? err.trim() : ""
      root.refreshState()
      root.refreshCameras()
    }
  }

  // --- bar button ---
  BarIconButton {
    id: button
    anchors.fill: parent
    bar: root.bar
    text: ""
    dimmed: !root.connected
    tooltipText: {
      var head = root.statusMeta
      if (root.running) head += " · " + root.cameraLabel + " · zoom " + root.selZoom + " · " + root.selSize + "@" + root.selFps
      return "OnePlus Webcam: " + head
    }
    onPressed: function(buttonCode) {
      if (buttonCode !== Qt.MiddleButton) root.toggle()
    }
  }

  Rectangle {
    visible: root.running
    anchors.right: parent.right
    anchors.top: parent.top
    width: 6
    height: 6
    radius: 3
    color: "#4ade80"
  }

  // --- popup panel ---
  KeyboardPanel {
    id: panel
    anchorItem: button
    owner: root
    bar: root.bar
    open: root.opened
    focusTarget: keyCatcher
    contentWidth: panel.fittedContentWidth(Style.space(360))
    contentHeight: panel.fittedContentHeight(column.implicitHeight, Style.space(560))

    PanelKeyCatcher {
      id: keyCatcher
      anchors.fill: parent
      onCloseRequested: root.close()
      onTabRequested: function(direction) { root.switchPanel(direction) }
      onTextKey: function(t) {
        if (t === "s" || t === "S") root.running ? root.doStop() : root.doStart()
        else if (t === "r" || t === "R") { root.refreshState(); root.refreshCameras() }
      }

      Flickable {
        id: panelFlick
        anchors.fill: parent
        contentWidth: width
        contentHeight: column.implicitHeight
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        flickableDirection: Flickable.VerticalFlick
        interactive: contentHeight > height
        ScrollBar.vertical: ScrollBar { policy: ScrollBar.AsNeeded }

        Column {
          id: column
          width: panelFlick.width
          spacing: Style.space(12)

          PanelHero {
            width: parent.width
            title: "OnePlus Webcam"
            meta: root.statusMeta
            foreground: root.foreground
            fontFamily: root.fontFamily
            iconComponent: Component {
              Item {
                width: Style.font.display
                height: Style.font.display
                Text {
                  anchors.centerIn: parent
                  text: ""
                  color: root.foreground
                  font.family: root.fontFamily
                  font.pixelSize: Style.font.display
                }
              }
            }
            trailingControl: Component {
              PanelActionButton {
                id: refreshBtn
                iconText: ""
                foreground: root.foreground
                fontFamily: root.fontFamily
                property bool tip: false
                onClicked: { root.refreshState(); root.refreshCameras() }
                HoverHandler {
                  onHoveredChanged: refreshBtn.tip = hovered
                }
                PanelToolTip {
                  visible: refreshBtn.tip
                  text: "Refresh"
                  fontFamily: root.fontFamily
                }
              }
            }
          }

          Text {
            width: parent.width
            text: {
              if (!root.connected) return "No phone detected. Plug in the USB cable and accept the debug prompt."
              if (!root.authorized) return "Phone connected but not authorized. Accept the prompt on the phone."
              if (root.running) return "Running · " + root.cameraLabel + " · zoom " + root.selZoom + " · " + root.selSize + "@" + root.selFps + "fps" + (root.previewWindow ? " · preview" : "")
              return "Stopped."
            }
            color: root.dim
            font.family: root.fontFamily
            font.pixelSize: Style.font.body
            wrapMode: Text.WordWrap
          }

          Text {
            visible: root.camError !== ""
            width: parent.width
            text: root.camError
            color: root.urgent
            font.family: root.fontFamily
            font.pixelSize: Style.font.bodySmall
            wrapMode: Text.WordWrap
          }

          PanelSectionHeader {
            text: "Camera"
            foreground: root.foreground
            fontFamily: root.fontFamily
          }

          Dropdown {
            id: cameraDropdown
            width: parent.width
            label: "Lens"
            value: root.selCamera
            options: {
              var opts = []
              for (var i = 0; i < root.cameras.length; i++)
                opts.push({ value: String(root.cameras[i].id), label: "Camera " + root.cameras[i].id + " (" + root.cameras[i].facing + ")" })
              if (opts.length === 0)
                opts = [{ value: "0", label: "Camera 0 (back)" }, { value: "1", label: "Camera 1 (front)" }, { value: "2", label: "Camera 2 (back)" }, { value: "3", label: "Camera 3 (back)" }]
              return opts
            }
            foreground: root.foreground
            fontFamily: root.fontFamily
            onChanged: function(v) {
              root.selCamera = v
              if (root.selZoom > root.zoomMax) root.selZoom = root.zoomMax
            }
          }

          PanelSectionHeader {
            text: "Capture"
            foreground: root.foreground
            fontFamily: root.fontFamily
          }

          Dropdown {
            id: sizeDropdown
            width: parent.width
            label: "Size"
            value: root.selSize
            options: ["3840x2160", "1920x1080", "1280x720", "640x480"]
            foreground: root.foreground
            fontFamily: root.fontFamily
            onChanged: function(v) { root.selSize = v }
          }

          NumberField {
            id: fpsField
            label: "FPS"
            value: root.selFps
            from: 10
            to: 60
            stepSize: 5
            fieldWidth: Style.space(200)
            foreground: root.foreground
            fontFamily: root.fontFamily
            onModified: function(v) { root.selFps = v }
          }

          NumberField {
            id: zoomField
            label: "Zoom"
            value: root.selZoom
            from: 1
            to: root.zoomMax
            stepSize: 1
            fieldWidth: Style.space(200)
            foreground: root.foreground
            fontFamily: root.fontFamily
            onModified: function(v) { root.selZoom = v }
          }

          PanelSectionHeader {
            text: "Control"
            foreground: root.foreground
            fontFamily: root.fontFamily
          }

          Button {
            id: startStopButton
            width: parent.width
            text: root.running ? "Stop webcam" : "Start webcam"
            iconText: root.running ? "\uf04d" : "\uf04b"
            selected: root.running
            bordered: true
            foreground: root.foreground
            fontFamily: root.fontFamily
            enabled: root.connected && root.authorized && !root.busy
            opacity: root.connected && root.authorized ? 1.0 : 0.5
            onClicked: root.running ? root.doStop() : root.doStart()
          }

          Toggle {
            width: parent.width
            label: "With preview"
            description: "Show a camera preview window (default: headless)"
            checked: root.previewWindow
            foreground: root.foreground
            fontFamily: root.fontFamily
            onClicked: root.previewWindow = !root.previewWindow
          }

          Text {
            visible: root.busy
            width: parent.width
            text: "Working…"
            color: root.dim
            font.family: root.fontFamily
            font.pixelSize: Style.font.caption
            horizontalAlignment: Text.AlignHCenter
          }
        }
      }
    }
  }
}
