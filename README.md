# MavlinkDeviceServer

`MavlinkDeviceServer` is a .NET 10 console application that exposes MAVLink
components for companion-computer gimbal integration. It currently uses an
in-memory fake gimbal so the MAVLink side can be exercised with ArduPilot SITL,
mavlink-router, Mission Planner, Wireshark, and the included injector before a
Skydroid C12 adapter is introduced.

## Current component model

All application components use system ID `1`:

| Component | ID | Current role |
| --- | ---: | --- |
| Onboard computer | 191 | HEARTBEAT and `ONBOARD_COMPUTER_STATUS` |
| Gimbal | 154 | MAVLink Gimbal v2 device with a fake backend |

The camera component remains in the source tree as a placeholder, but is not
registered or advertised by default.

For normal control, ArduPilot is the Gimbal Manager and sends
`GIMBAL_DEVICE_SET_ATTITUDE` to the gimbal component. The gimbal reports
`GIMBAL_DEVICE_INFORMATION` and `GIMBAL_DEVICE_ATTITUDE_STATUS`.
Component `1/154` does not implement Gimbal Manager messages. The injector can
still send `GIMBAL_MANAGER_SET_ATTITUDE` to an external manager such as
ArduPilot for manager-path testing.

## Quick start

Prerequisites: .NET SDK 10, ArduPilot SITL, and mavlink-router.

For a concrete ArduPlane SITL invocation, see [launch-sitl.sh](launch-sitl.sh).
It assumes a local ArduPilot checkout and the mavlink-router UDP input at
`127.0.0.1:14550`; the accompanying [SITL guide](docs/guides/sitl.md)
explains its path and defaults-file assumptions.

```powershell
dotnet restore MavlinkDeviceServer.slnx
dotnet build MavlinkDeviceServer.slnx
dotnet run --project MavlinkDeviceServer.csproj
```

The server listens on UDP port `14551`, learns the remote endpoint from inbound
traffic, and writes a debug log under `logs/`.

Run a deterministic one-shot gimbal device test through mavlink-router's
injector endpoint:

```powershell
dotnet run --project tools/MavlinkInjector -- `
  gimbal-device-set-attitude `
  --pitch -30 `
  --yaw 45
```

## Documentation

Project documentation lives in `docs/` and is published through GitHub Pages by
`.github/workflows/docs.yml`. MkDocs is installed only on the GitHub Actions
runner; it is not a local project dependency.

- [Documentation home](docs/index.md)
- [SITL setup](docs/guides/sitl.md)
- [mavlink-router setup](docs/guides/mavlink-router.md)
- [Gimbal device integration](docs/guides/gimbal-device.md)
- [Injector usage](docs/guides/injector.md)

Configure **Settings → Pages → Build and deployment** to use **GitHub Actions**.
The workflow deploys after a push to `master` or when manually dispatched.

## Important current limitation

The learned UDP endpoint is intentional for local testing. A packet from a
temporary sender can replace the destination for periodic telemetry. Before
production deployment, configure an explicit mavlink-router destination.

## License

See [LICENSE](LICENSE).
