using CommandLine;

namespace MavlinkInjector.Commands;

public abstract class CommonInjectorOptions
{
    [Option("host", Default = "127.0.0.1", HelpText = "Destination IP address.")]
    public string Host { get; set; } = "127.0.0.1";

    [Option("port", Default = (ushort)14552, HelpText = "Destination UDP port.")]
    public ushort Port { get; set; } = 14552;

    [Option("source-system", Default = (byte)255, HelpText = "Source MAVLink system ID.")]
    public byte SourceSystem { get; set; } = 255;

    [Option("source-component", Default = (byte)190, HelpText = "Source MAVLink component ID.")]
    public byte SourceComponent { get; set; } = 190;

    [Option("target-system", Default = (byte)1, HelpText = "Target MAVLink system ID.")]
    public byte TargetSystem { get; set; } = 1;

    [Option("target-component", Default = (byte)154, HelpText = "Target MAVLink component ID.")]
    public byte TargetComponent { get; set; } = 154;

    public CommonInjectorOptions ApplyConfiguredDefaults(
        InjectorDefaults defaults,
        ISet<string> suppliedOptions)
    {
        if (!suppliedOptions.Contains("host")) Host = defaults.Host;
        if (!suppliedOptions.Contains("port")) Port = defaults.Port;
        if (!suppliedOptions.Contains("source-system")) SourceSystem = defaults.SourceSystem;
        if (!suppliedOptions.Contains("source-component")) SourceComponent = defaults.SourceComponent;
        if (!suppliedOptions.Contains("target-system")) TargetSystem = defaults.TargetSystem;
        if (!suppliedOptions.Contains("target-component")) TargetComponent = defaults.TargetComponent;
        return this;
    }
}
