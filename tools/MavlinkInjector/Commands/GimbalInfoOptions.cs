using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-info", HelpText = "Request GIMBAL_DEVICE_INFORMATION from the gimbal.")]
public sealed class GimbalInfoOptions : CommonInjectorOptions;
