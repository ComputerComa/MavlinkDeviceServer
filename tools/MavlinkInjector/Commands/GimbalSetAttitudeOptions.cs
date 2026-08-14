using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-set-attitude", HelpText = "Set the fake gimbal attitude in degrees.")]
public sealed class GimbalSetAttitudeOptions : CommonInjectorOptions
{
    [Option("pitch", Required = true, HelpText = "Requested pitch in degrees.")]
    public float Pitch { get; set; }

    [Option("yaw", Required = true, HelpText = "Requested yaw in degrees.")]
    public float Yaw { get; set; }

    [Option("roll", Default = 0f, HelpText = "Requested roll in degrees.")]
    public float Roll { get; set; }
}
