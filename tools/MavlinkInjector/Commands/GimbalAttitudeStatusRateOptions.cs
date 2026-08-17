using CommandLine;

namespace MavlinkInjector.Commands;

[Verb("gimbal-attitude-status-rate", HelpText = "Set the GIMBAL_DEVICE_ATTITUDE_STATUS stream interval in microseconds.")]
public sealed class GimbalAttitudeStatusRateOptions : CommonInjectorOptions
{
    [Option("interval-us", Required = true, HelpText = "Positive interval, zero for default, or negative to stop the stream.")]
    public long IntervalMicroseconds { get; set; }
}
