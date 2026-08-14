# Injector configuration

The injector has built-in defaults for the local mavlink-router test topology:

```text
host             127.0.0.1
port             14552
source system    255
source component 190
target system    1
target component 154
```

To override these defaults for a machine or network, copy
`tools/MavlinkInjector/mavlink-injector.json.example` to
`mavlink-injector.json` in the directory where you run the injector.

```json
{
  "MavlinkInjector": {
    "Host": "192.168.1.50",
    "Port": 14552,
    "TargetSystem": 1,
    "TargetComponent": 154
  }
}
```

Any CLI option takes precedence over the configuration file:

```powershell
dotnet run --project tools/MavlinkInjector -- `
  gimbal-info `
  --host 127.0.0.1 `
  --target-component 154
```

The file is optional. Invalid numeric values cause a concise error and a
nonzero exit code.
