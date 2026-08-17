# MAVLink injector

`tools/MavlinkInjector` sends deterministic one-shot packets without a heartbeat
or automatic telemetry requests.

```powershell
dotnet run --project tools/MavlinkInjector -- --help
dotnet run --project tools/MavlinkInjector -- gimbal-info
```

Device-level attitude test:

```powershell
dotnet run --project tools/MavlinkInjector -- `
  gimbal-device-set-attitude `
  --pitch -30 `
  --yaw 45 `
  --vehicle-frame
```

Earth-frame/lock test:

```powershell
dotnet run --project tools/MavlinkInjector -- `
  gimbal-device-set-attitude `
  --pitch -30 `
  --yaw 45 `
  --earth-frame `
  --roll-lock `
  --pitch-lock `
  --yaw-lock
```

Set the direct device-status stream to 5 Hz, restore its default 10 Hz rate,
or disable it:

```powershell
dotnet run --project tools/MavlinkInjector -- gimbal-attitude-status-rate --interval-us 200000
dotnet run --project tools/MavlinkInjector -- gimbal-attitude-status-rate --interval-us 0
dotnet run --project tools/MavlinkInjector -- gimbal-attitude-status-rate --interval-us -1
```

Defaults are router injector `127.0.0.1:14552`, source `255/190`, and target
`1/154`.

`gimbal-device-set-attitude` is the direct isolated test of the device server
at `1/154`. `gimbal-set-attitude` and `gimbal-center` send the manager-level
message 282; use `--target-component 1` when testing the ArduPilot manager.
The device server intentionally does not consume message 282.
