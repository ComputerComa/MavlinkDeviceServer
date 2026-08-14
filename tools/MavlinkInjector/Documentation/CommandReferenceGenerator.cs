using System.Reflection;
using System.Text;
using CommandLine;
using MavlinkInjector.Commands;

namespace MavlinkInjector.Documentation;

public static class CommandReferenceGenerator
{
    public static void Write(string path)
    {
        var builder = new StringBuilder("# MAVLink Injector command reference\n\n");
        builder.AppendLine("Generated from the CommandLineParser option classes. Do not edit manually.\n");
        var commandTypes = typeof(GimbalInfoOptions).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(CommonInjectorOptions).IsAssignableFrom(type))
            .Select(type => (Type: type, Verb: type.GetCustomAttribute<VerbAttribute>()))
            .Where(item => item.Verb is not null)
            .OrderBy(item => item.Verb!.Name, StringComparer.Ordinal);

        foreach (var (type, verb) in commandTypes)
        {
            builder.AppendLine($"## `{verb!.Name}`\n");
            builder.AppendLine(verb.HelpText);
            builder.AppendLine();
            builder.AppendLine("```text");
            builder.AppendLine($"mavlink-injector {verb.Name} [options]");
            builder.AppendLine("```\n");
            builder.AppendLine("| Option | Required | Default | Description |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var property in type.GetProperties().OrderBy(property => property.DeclaringType == typeof(CommonInjectorOptions) ? 1 : 0))
            {
                var option = property.GetCustomAttribute<OptionAttribute>();
                if (option is null) continue;
                var fallback = property.DeclaringType == typeof(CommonInjectorOptions) ? "configuration or built-in" : option.Default?.ToString() ?? "";
                builder.AppendLine($"| `--{option.LongName}` | {(option.Required ? "yes" : "no")} | {fallback} | {option.HelpText} |");
            }
            builder.AppendLine();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, builder.ToString());
    }
}
