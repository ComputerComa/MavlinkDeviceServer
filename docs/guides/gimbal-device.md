# Gimbal device integration

The server exposes `SYSID 1 / COMPID 154 / MAV_TYPE_GIMBAL`. ArduPilot is the
normal Gimbal Manager and sends low-level setpoints to this device.

| Message | Direction | Behavior |
| --- | --- | --- |
| `GIMBAL_DEVICE_INFORMATION` (283) | Server → network | Request-driven limits/capabilities |
| `GIMBAL_DEVICE_SET_ATTITUDE` (284) | ArduPilot → server | Applies fake attitude/rate targets |
| `GIMBAL_DEVICE_ATTITUDE_STATUS` (285) | Server → network | Periodic fake-state report |
| `AUTOPILOT_STATE_FOR_GIMBAL_DEVICE` (286) | ArduPilot → server | Stored for future stabilization work |

Default telemetry rates are 1 Hz for the device heartbeat and 10 Hz for
`GIMBAL_DEVICE_ATTITUDE_STATUS`. The gimbal handles
`MAV_CMD_SET_MESSAGE_INTERVAL` (511) for message 285 only:

- Positive `param2` values set an interval in microseconds, from 10,000 us
  (100 Hz) through 60,000,000 us (60 seconds).
- `param2 = 0` restores the default 100,000 us interval (10 Hz).
- Negative `param2` values stop the periodic 285 stream.
- Invalid intervals receive `MAV_RESULT_FAILED`; other message IDs receive
  `MAV_RESULT_UNSUPPORTED`.

The fake backend accepts roll/pitch/yaw lock flags, either yaw frame, and the
`RETRACT` and `NEUTRAL` device flags. Retract and neutral are both represented
by a centered attitude for now, while the periodic status flags retain the
requested position. It does not implement RC input, geographic calculations,
stabilization, or physical motion simulation.

Mission Planner mount buttons send high-level commands such as
`MAV_CMD_DO_MOUNT_CONTROL` (205) to ArduPilot `1/1`, not to this server.
ArduPilot must discover/configure component `1/154` as its gimbal device and
translate those requests into `GIMBAL_DEVICE_SET_ATTITUDE` (284). Verify the
downstream 284 setpoints and matching 285 reports in Wireshark.

## Expected discovery sequence

```text
1/154 HEARTBEAT
  -> ArduPilot requests GIMBAL_DEVICE_INFORMATION (283)
  -> 1/154 returns GIMBAL_DEVICE_INFORMATION
  -> ArduPilot sends AUTOPILOT_STATE_FOR_GIMBAL_DEVICE (286)
  -> ArduPilot sends GIMBAL_DEVICE_SET_ATTITUDE (284)
  -> 1/154 reports GIMBAL_DEVICE_ATTITUDE_STATUS (285)
```

Useful Wireshark display filters:

```text
mavlink.msgid == 283
mavlink.msgid == 284 || mavlink.msgid == 285 || mavlink.msgid == 286
udp.port == 14551 && (mavlink.msgid >= 283 && mavlink.msgid <= 286)
```
