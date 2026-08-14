# Architecture

| MAVLink identity | Owner | Purpose |
| --- | --- | --- |
| `1/1` | ArduPilot | Flight controller and normal Gimbal Manager |
| `1/191` | MavlinkDeviceServer | Onboard computer telemetry |
| `1/154` | MavlinkDeviceServer | Gimbal device |

`1/100` is reserved for future camera work but is not registered or advertised.

```text
UDP -> MavlinkServer -> MavlinkMessageDispatcher -> ComponentRegistry
    -> GimbalComponent -> IGimbalDevice -> FakeGimbalDevice
```

The server learns its telemetry destination from inbound traffic for testing.
Production deployment must use an explicit mavlink-router destination.
