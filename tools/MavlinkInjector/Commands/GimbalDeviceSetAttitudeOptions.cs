using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-device-set-attitude", HelpText = "Send a device-level gimbal attitude setpoint in degrees.")]
public sealed class GimbalDeviceSetAttitudeOptions : CommonInjectorOptions
{
    [Option("pitch", Required = true, HelpText = "Requested pitch in degrees.")]
    public float Pitch { get; set; }

    [Option("yaw", Required = true, HelpText = "Requested yaw in degrees.")]
    public float Yaw { get; set; }

    [Option("roll", Default = 0f, HelpText = "Requested roll in degrees.")]
    public float Roll { get; set; }

    [Option("earth-frame", HelpText = "Set yaw in the earth frame.")]
    public bool EarthFrame { get; set; }

    [Option("vehicle-frame", HelpText = "Set yaw in the vehicle frame (the default).")]
    public bool VehicleFrame { get; set; }

    [Option("roll-lock", HelpText = "Set the roll-lock flag.")]
    public bool RollLock { get; set; }

    [Option("pitch-lock", HelpText = "Set the pitch-lock flag.")]
    public bool PitchLock { get; set; }

    [Option("yaw-lock", HelpText = "Set the legacy yaw-lock flag.")]
    public bool YawLock { get; set; }
}
