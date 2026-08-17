using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-center", HelpText = "Send a zero-attitude GIMBAL_MANAGER_SET_ATTITUDE command to an external gimbal manager.")]
public sealed class GimbalCenterOptions : CommonInjectorOptions;
