# ArduPilot SITL setup

Start ArduPilot SITL with MAVLink traffic sent to mavlink-router UDP port `14550`.
Load [sitl-minimal.param](../../sitl-minimal.param) through your normal SITL
parameter workflow and verify:

```text
MNT1_TYPE = 6
```

## Included ArduPlane launch example

[launch-sitl.sh](../../launch-sitl.sh) is a concrete ArduPlane SITL launch
example for this topology. It starts the built `arduplane` binary with the
Plane model and sends MAVLink to mavlink-router at `127.0.0.1:14550` through
`--serial0=udpclient:127.0.0.1:14550`.

Run it from the repository with Bash after adjusting the local paths if
needed:

```sh
bash launch-sitl.sh
```

The script assumes an ArduPilot checkout reachable as `~ardupilot` and a
defaults file at `~/sitl-minimal.param`. Copy the repository's
[sitl-minimal.param](../../sitl-minimal.param) to that location, or update the
script's `--defaults` value for your environment.

After SITL, mavlink-router, and MavlinkDeviceServer start, observe:

```text
1/1    ArduPilot flight controller
1/191  MavlinkDeviceServer onboard computer
1/154  MavlinkDeviceServer gimbal
```

Mission Planner Home/ROI/mount actions should make ArduPilot emit repeated
`GIMBAL_DEVICE_SET_ATTITUDE` (284) packets to `1/154`.
