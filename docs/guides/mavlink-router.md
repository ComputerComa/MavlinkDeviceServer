# mavlink-router setup

The checked-in [mavlink-router.cfg](https://github.com/ComputerComa/MavlinkDeviceServer/blob/master/mavlink-router.cfg) documents the
local test topology.

| Endpoint | Port | Role |
| --- | ---: | --- |
| SITL | 14550 | ArduPilot traffic enters the router |
| DeviceServer | 14551 | MavlinkDeviceServer listener |
| Injector | 14552 | One-shot test injection |
| GCS | 14553 | Mission Planner or another GCS |

Update the `DeviceServer` address with the server computer IP. For a local test,
use `127.0.0.1`.

```powershell
mavlink-routerd -c mavlink-router.cfg
```

The server currently transmits periodic telemetry to the most recent inbound
sender. This is test-only behavior; production needs an explicit router target.
