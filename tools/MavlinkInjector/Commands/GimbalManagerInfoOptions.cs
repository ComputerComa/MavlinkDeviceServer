using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-manager-info", HelpText = "Request GIMBAL_MANAGER_INFORMATION from the gimbal.")]
public sealed class GimbalManagerInfoOptions : CommonInjectorOptions;
