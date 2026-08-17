using CommandLine;
using MavlinkInjector.Commands;
using MavlinkInjector.Documentation;

if (args.FirstOrDefault() == "--generate-command-reference")
{
    CommandReferenceGenerator.Write(args.ElementAtOrDefault(1) ?? "docs/reference/mavlink-injector.md");
    return 0;
}

InjectorDefaults defaults;
try { defaults = InjectorDefaults.Load(); }
catch (Exception exception) { Console.Error.WriteLine(exception.Message); return 2; }
var suppliedOptions = InjectorDefaults.GetSuppliedCommonOptions(args);

return await Parser.Default
    .ParseArguments<GimbalInfoOptions, GimbalSetAttitudeOptions, GimbalDeviceSetAttitudeOptions, GimbalAttitudeStatusRateOptions, GimbalCenterOptions>(args)
    .MapResult<GimbalInfoOptions, GimbalSetAttitudeOptions, GimbalDeviceSetAttitudeOptions, GimbalAttitudeStatusRateOptions, GimbalCenterOptions, Task<int>>(
        options => InjectorCommands.RunGimbalInfoAsync((GimbalInfoOptions)options.ApplyConfiguredDefaults(defaults, suppliedOptions)),
        options => InjectorCommands.RunGimbalSetAttitudeAsync((GimbalSetAttitudeOptions)options.ApplyConfiguredDefaults(defaults, suppliedOptions)),
        options => InjectorCommands.RunGimbalDeviceSetAttitudeAsync((GimbalDeviceSetAttitudeOptions)options.ApplyConfiguredDefaults(defaults, suppliedOptions)),
        options => InjectorCommands.RunGimbalAttitudeStatusRateAsync((GimbalAttitudeStatusRateOptions)options.ApplyConfiguredDefaults(defaults, suppliedOptions)),
        options => InjectorCommands.RunGimbalCenterAsync((GimbalCenterOptions)options.ApplyConfiguredDefaults(defaults, suppliedOptions)),
        errors => Task.FromResult(errors.Any(error => error is HelpRequestedError or VersionRequestedError) ? 0 : 2));
