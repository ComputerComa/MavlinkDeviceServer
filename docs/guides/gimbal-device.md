# Gimbal device integration

The server exposes `SYSID 1 / COMPID 154 / MAV_TYPE_GIMBAL`. ArduPilot is the
normal Gimbal Manager and sends low-level setpoints to this device.

| Message | Direction | Behavior |
| --- | --- | --- |
| `GIMBAL_DEVICE_INFORMATION` (283) | Server → network | Request-driven limits/capabilities |
| `GIMBAL_DEVICE_SET_ATTITUDE` (284) | ArduPilot → server | Applies fake attitude/rate targets |
| `GIMBAL_DEVICE_ATTITUDE_STATUS` (285) | Server → network | Periodic fake-state report |
| `AUTOPILOT_STATE_FOR_GIMBAL_DEVICE` (286) | ArduPilot → server | Stored for future stabilization work |

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
