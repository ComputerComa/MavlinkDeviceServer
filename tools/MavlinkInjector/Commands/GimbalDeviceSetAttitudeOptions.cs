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
}
