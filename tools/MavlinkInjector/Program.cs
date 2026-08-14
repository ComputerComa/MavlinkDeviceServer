using CommandLine;
using MavlinkInjector.Commands;

return await Parser.Default
    .ParseArguments<GimbalInfoOptions, GimbalManagerInfoOptions, GimbalSetAttitudeOptions, GimbalDeviceSetAttitudeOptions, GimbalCenterOptions>(args)
    .MapResult<GimbalInfoOptions, GimbalManagerInfoOptions, GimbalSetAttitudeOptions, GimbalDeviceSetAttitudeOptions, GimbalCenterOptions, Task<int>>(
        options => InjectorCommands.RunGimbalInfoAsync(options),
        options => InjectorCommands.RunGimbalManagerInfoAsync(options),
        options => InjectorCommands.RunGimbalSetAttitudeAsync(options),
        options => InjectorCommands.RunGimbalDeviceSetAttitudeAsync(options),
        options => InjectorCommands.RunGimbalCenterAsync(options),
        errors => Task.FromResult(errors.Any(error => error is HelpRequestedError or VersionRequestedError) ? 0 : 2));
