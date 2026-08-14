using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-center", HelpText = "Center the fake gimbal.")]
public sealed class GimbalCenterOptions : CommonInjectorOptions;
