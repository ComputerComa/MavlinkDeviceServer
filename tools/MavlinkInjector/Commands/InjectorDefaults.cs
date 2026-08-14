using Microsoft.Extensions.Configuration;

namespace MavlinkInjector.Commands;

public sealed record InjectorDefaults(
    string Host,
    ushort Port,
    byte SourceSystem,
    byte SourceComponent,
    byte TargetSystem,
    byte TargetComponent)
{
    public static InjectorDefaults Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("mavlink-injector.json", optional: true)
            .Build();
        var section = configuration.GetSection("MavlinkInjector");
        return new InjectorDefaults(
            section["Host"] ?? "127.0.0.1",
            Parse(section["Port"], (ushort)14552, "Port"),
            Parse(section["SourceSystem"], (byte)255, "SourceSystem"),
            Parse(section["SourceComponent"], (byte)190, "SourceComponent"),
            Parse(section["TargetSystem"], (byte)1, "TargetSystem"),
            Parse(section["TargetComponent"], (byte)154, "TargetComponent"));
    }

    public static ISet<string> GetSuppliedCommonOptions(IEnumerable<string> arguments) =>
        arguments.Where(argument => argument.StartsWith("--", StringComparison.Ordinal))
            .Select(argument => argument[2..].Split('=', 2)[0])
            .Where(argument => argument is "host" or "port" or "source-system" or "source-component" or "target-system" or "target-component")
            .ToHashSet(StringComparer.Ordinal);

    private static T Parse<T>(string? value, T fallback, string name) where T : struct, IParsable<T> =>
        value is null ? fallback : T.TryParse(value, null, out var result)
            ? result
            : throw new InvalidOperationException($"Invalid {name} in mavlink-injector.json: '{value}'.");
}
