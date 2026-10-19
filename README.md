# MonitorSetup
App used for managing displays: turn individual monitors on and off without unplugging them, and change everything the Windows **Display** settings page offers.

## Features
- List every connected display, including ones that are currently disabled
- Enable / disable a single display (it comes back with the same position, resolution and refresh rate)
- Drag-and-drop arrangement with edge snapping
- Main (primary) display
- Resolution, refresh rate and orientation, with a 15 second "Keep these settings?" auto-revert
- Scale (100%, 125%, ...) per display
- HDR on/off where supported
- Projection mode: extend, duplicate, PC screen only, second screen only
- Identify (shows each display's number on screen)

## Build
Requires the .NET 8 SDK (or newer) on Windows 10/11.

```
dotnet build MonitorSetup.sln -c Release
```

The app is at `src/MonitorSetup/bin/Release/net8.0-windows/MonitorSetup.exe`.

## Command line
The same exe works from scripts and shortcuts:

```
MonitorSetup list
MonitorSetup disable 3
MonitorSetup enable 3
MonitorSetup mode 1 3840 2160 60
MonitorSetup move 2 3840 0
MonitorSetup primary 2
```

## How it works
Displays are read and switched on/off with the CCD API (`QueryDisplayConfig` / `SetDisplayConfig`).
Resolution, refresh rate, orientation, position and primary display use `ChangeDisplaySettingsEx`.
Scale uses the same (undocumented) `DisplayConfigSetDeviceInfo` call as the Windows Settings app.
