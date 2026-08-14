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

Defaults are router injector `127.0.0.1:14552`, source `255/190`, and target
`1/154`.
