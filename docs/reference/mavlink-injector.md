# MAVLink Injector command reference

Generated from the CommandLineParser option classes. Do not edit manually.

## `gimbal-attitude-status-rate`

Set the GIMBAL_DEVICE_ATTITUDE_STATUS stream interval in microseconds.

```text
mavlink-injector gimbal-attitude-status-rate [options]
```

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `--interval-us` | yes |  | Positive interval, zero for default, or negative to stop the stream. |
| `--host` | no | configuration or built-in | Destination IP address. |
| `--port` | no | configuration or built-in | Destination UDP port. |
| `--source-system` | no | configuration or built-in | Source MAVLink system ID. |
| `--source-component` | no | configuration or built-in | Source MAVLink component ID. |
| `--target-system` | no | configuration or built-in | Target MAVLink system ID. |
| `--target-component` | no | configuration or built-in | Target MAVLink component ID. |

## `gimbal-center`

Send a zero-attitude GIMBAL_MANAGER_SET_ATTITUDE command to an external gimbal manager.

```text
mavlink-injector gimbal-center [options]
```

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `--host` | no | configuration or built-in | Destination IP address. |
| `--port` | no | configuration or built-in | Destination UDP port. |
| `--source-system` | no | configuration or built-in | Source MAVLink system ID. |
| `--source-component` | no | configuration or built-in | Source MAVLink component ID. |
| `--target-system` | no | configuration or built-in | Target MAVLink system ID. |
| `--target-component` | no | configuration or built-in | Target MAVLink component ID. |

## `gimbal-device-set-attitude`

Send a device-level gimbal attitude setpoint in degrees.

```text
mavlink-injector gimbal-device-set-attitude [options]
```

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `--pitch` | yes |  | Requested pitch in degrees. |
| `--yaw` | yes |  | Requested yaw in degrees. |
| `--roll` | no | 0 | Requested roll in degrees. |
| `--earth-frame` | no |  | Set yaw in the earth frame. |
| `--vehicle-frame` | no |  | Set yaw in the vehicle frame (the default). |
| `--roll-lock` | no |  | Set the roll-lock flag. |
| `--pitch-lock` | no |  | Set the pitch-lock flag. |
| `--yaw-lock` | no |  | Set the legacy yaw-lock flag. |
| `--host` | no | configuration or built-in | Destination IP address. |
| `--port` | no | configuration or built-in | Destination UDP port. |
| `--source-system` | no | configuration or built-in | Source MAVLink system ID. |
| `--source-component` | no | configuration or built-in | Source MAVLink component ID. |
| `--target-system` | no | configuration or built-in | Target MAVLink system ID. |
| `--target-component` | no | configuration or built-in | Target MAVLink component ID. |

## `gimbal-info`

Request GIMBAL_DEVICE_INFORMATION from the gimbal.

```text
mavlink-injector gimbal-info [options]
```

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `--host` | no | configuration or built-in | Destination IP address. |
| `--port` | no | configuration or built-in | Destination UDP port. |
| `--source-system` | no | configuration or built-in | Source MAVLink system ID. |
| `--source-component` | no | configuration or built-in | Source MAVLink component ID. |
| `--target-system` | no | configuration or built-in | Target MAVLink system ID. |
| `--target-component` | no | configuration or built-in | Target MAVLink component ID. |

## `gimbal-set-attitude`

Send GIMBAL_MANAGER_SET_ATTITUDE to an external gimbal manager.

```text
mavlink-injector gimbal-set-attitude [options]
```

| Option | Required | Default | Description |
| --- | --- | --- | --- |
| `--pitch` | yes |  | Requested pitch in degrees. |
| `--yaw` | yes |  | Requested yaw in degrees. |
| `--roll` | no | 0 | Requested roll in degrees. |
| `--host` | no | configuration or built-in | Destination IP address. |
| `--port` | no | configuration or built-in | Destination UDP port. |
| `--source-system` | no | configuration or built-in | Source MAVLink system ID. |
| `--source-component` | no | configuration or built-in | Source MAVLink component ID. |
| `--target-system` | no | configuration or built-in | Target MAVLink system ID. |
| `--target-component` | no | configuration or built-in | Target MAVLink component ID. |

