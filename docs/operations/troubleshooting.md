# Troubleshooting

## No periodic telemetry

The server does not transmit until it receives a UDP packet and learns a remote
endpoint. Confirm mavlink-router forwards to UDP `14551`.

## ArduPilot does not send 284

Check `MNT1_TYPE = 6`, the `1/154` gimbal heartbeat, and bidirectional router
forwarding. Mission Planner should target ArduPilot, not the gimbal device.

## Windows UDP reset 10054

Windows can surface ICMP port-unreachable as a UDP receive reset. The server
logs it and continues listening; verify router endpoints if it repeats.
