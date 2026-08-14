# MavlinkDeviceServer

MavlinkDeviceServer is a .NET 10 companion-computer MAVLink Gimbal v2 device.
The current backend is `FakeGimbalDevice`; a future C12 adapter will replace it
without changing the MAVLink identity or transport topology.

## Control path

```text
Mission Planner / mission / RC / ROI
                |
                v
       ArduPilot Gimbal Manager
                |
       GIMBAL_DEVICE_SET_ATTITUDE (284)
                |
                v
 MavlinkDeviceServer 1/154 (gimbal device)
                |
                v
        IGimbalDevice -> FakeGimbalDevice
```

ArduPilot performs high-level mount, Home, ROI, POI, and mission targeting.
The device server consumes the resulting low-level gimbal-device setpoints.
