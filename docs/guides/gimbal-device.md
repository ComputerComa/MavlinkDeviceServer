# Gimbal device integration

The server exposes `SYSID 1 / COMPID 154 / MAV_TYPE_GIMBAL`. ArduPilot is the
normal Gimbal Manager and sends low-level setpoints to this device.

| Message | Direction | Behavior |
| --- | --- | --- |
| `GIMBAL_DEVICE_INFORMATION` (283) | Server → network | Request-driven limits/capabilities |
| `GIMBAL_DEVICE_SET_ATTITUDE` (284) | ArduPilot → server | Applies fake attitude/rate targets |
| `GIMBAL_DEVICE_ATTITUDE_STATUS` (285) | Server → network | Periodic fake-state report |
| `AUTOPILOT_STATE_FOR_GIMBAL_DEVICE` (286) | ArduPilot → server | Stored for future stabilization work |

The fake backend accepts roll/pitch/yaw lock flags and either yaw frame. It does
not implement retract, neutral, RC, geographic calculations, or stabilization.

Send ROI and mount commands to ArduPilot, not directly to this server. Verify
changing 284 setpoints and matching 285 reports in Wireshark.
