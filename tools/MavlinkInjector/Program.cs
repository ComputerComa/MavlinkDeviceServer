using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Asv.Mavlink;
using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;

return await InjectorApplication.RunAsync(args);

internal static class InjectorApplication
{
    private static readonly IReadOnlyDictionary<string, IInjectorCommand> Commands =
        new Dictionary<string, IInjectorCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["gimbal-info"] = new GimbalInfoCommand(),
            ["gimbal-set-attitude"] = new GimbalSetAttitudeCommand("gimbal-set-attitude", false),
            ["gimbal-center"] = new GimbalSetAttitudeCommand("gimbal-center", true)
        };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return 0;
        }

        if (!Commands.TryGetValue(args[0], out var command))
        {
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            PrintHelp();
            return 2;
        }

        if (args.Length > 1 && IsHelp(args[1]))
        {
            command.PrintHelp();
            return 0;
        }

        if (!InjectorOptions.TryParse(args[1..], command.DefaultTargetComponent, out var options, out var error))
        {
            Console.Error.WriteLine($"Invalid arguments: {error}");
            command.PrintHelp();
            return 2;
        }

        try
        {
            var packet = command.CreatePacket(options);
            var frame = MavlinkEncoder.Encode(packet);
            command.VerifyPacket(frame, options);

            using var udp = new UdpClient();
            await udp.SendAsync(frame, new IPEndPoint(options.Host, options.Port));

            command.PrintSent(options);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to inject MAVLink command: {exception.Message}");
            return 1;
        }
    }

    private static bool IsHelp(string argument) => argument is "--help" or "-h";

    private static void PrintHelp()
    {
        Console.WriteLine("MAVLink Injector");
        Console.WriteLine("Usage: mavlink-injector <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  gimbal-info  Request GIMBAL_DEVICE_INFORMATION from a gimbal component.");
        Console.WriteLine();
        Console.WriteLine("Run 'mavlink-injector gimbal-info --help' for command options.");
    }
}

internal interface IInjectorCommand
{
    byte DefaultTargetComponent { get; }
    MavlinkV2Message CreatePacket(InjectorOptions options);
    void VerifyPacket(byte[] frame, InjectorOptions options);
    IReadOnlyList<ExpectedResponse> ExpectedResponses(InjectorOptions options);
    void PrintHelp();
    void PrintSent(InjectorOptions options);
}

internal sealed record ExpectedResponse(
    uint MessageId,
    byte SystemId,
    byte ComponentId,
    MavCmd? AcknowledgedCommand = null,
    MavResult? AcknowledgementResult = null);

internal sealed class GimbalInfoCommand : IInjectorCommand
{
    private const uint GimbalDeviceInformationMessageId = 283;
    public byte DefaultTargetComponent => 154;

    public MavlinkV2Message CreatePacket(InjectorOptions options)
    {
        var packet = new CommandLongPacket
        {
            SystemId = options.SourceSystem,
            ComponentId = options.SourceComponent,
            Sequence = 0
        };

        packet.Payload.TargetSystem = options.TargetSystem;
        packet.Payload.TargetComponent = options.TargetComponent;
        packet.Payload.Command = MavCmd.MavCmdRequestMessage;
        packet.Payload.Confirmation = 0;
        packet.Payload.Param1 = GimbalDeviceInformationMessageId;
        return packet;
    }

    public void VerifyPacket(byte[] frame, InjectorOptions options)
    {
        var decoded = new CommandLongPacket();
        ReadOnlySpan<byte> readSpan = frame;
        decoded.Deserialize(ref readSpan);

        if (decoded.SystemId != options.SourceSystem ||
            decoded.ComponentId != options.SourceComponent ||
            decoded.Payload.TargetSystem != options.TargetSystem ||
            decoded.Payload.TargetComponent != options.TargetComponent ||
            decoded.Payload.Command != MavCmd.MavCmdRequestMessage ||
            decoded.Payload.Confirmation != 0 ||
            decoded.Payload.Param1 != GimbalDeviceInformationMessageId ||
            decoded.Payload.Param2 != 0 || decoded.Payload.Param3 != 0 ||
            decoded.Payload.Param4 != 0 || decoded.Payload.Param5 != 0 ||
            decoded.Payload.Param6 != 0 || decoded.Payload.Param7 != 0)
        {
            throw new InvalidOperationException("Encoded COMMAND_LONG did not match the requested gimbal-info command.");
        }
    }

    public void PrintHelp()
    {
        Console.WriteLine("Usage: mavlink-injector gimbal-info [options]");
        Console.WriteLine();
        InjectorOptions.PrintHelp(DefaultTargetComponent);
    }

    public IReadOnlyList<ExpectedResponse> ExpectedResponses(InjectorOptions options) =>
    [
        new(GimbalDeviceInformationMessageId, options.TargetSystem, options.TargetComponent),
        new(77, options.TargetSystem, options.TargetComponent,
            MavCmd.MavCmdRequestMessage, MavResult.MavResultAccepted)
    ];

    public void PrintSent(InjectorOptions options)
    {
        Console.WriteLine("MAVLink Injector");
        Console.WriteLine($"Destination: {options.Host}:{options.Port}");
        Console.WriteLine($"Source:      {options.SourceSystem}/{options.SourceComponent}");
        Console.WriteLine($"Target:      {options.TargetSystem}/{options.TargetComponent}");
        Console.WriteLine("Command:     MAV_CMD_REQUEST_MESSAGE");
        Console.WriteLine("Requested:   GIMBAL_DEVICE_INFORMATION (283)");
        Console.WriteLine("Sent successfully.");
    }
}

internal sealed class GimbalSetAttitudeCommand(string name, bool center) : IInjectorCommand
{
    private const byte GimbalComponentId = 154;
    public byte DefaultTargetComponent => GimbalComponentId;

    public MavlinkV2Message CreatePacket(InjectorOptions options)
    {
        var (roll, pitch, yaw) = center ? (0f, 0f, 0f) :
            (options.RollDegrees, options.PitchDegrees, options.YawDegrees);
        var q = EulerDegreesToQuaternion(roll, pitch, yaw);
        var packet = new GimbalManagerSetAttitudePacket
        {
            SystemId = options.SourceSystem,
            ComponentId = options.SourceComponent,
            Sequence = 0
        };

        packet.Payload.TargetSystem = options.TargetSystem;
        packet.Payload.TargetComponent = options.TargetComponent;
        packet.Payload.GimbalDeviceId = 1;
        packet.Payload.Q[0] = q.W;
        packet.Payload.Q[1] = q.X;
        packet.Payload.Q[2] = q.Y;
        packet.Payload.Q[3] = q.Z;
        packet.Payload.AngularVelocityX = float.NaN;
        packet.Payload.AngularVelocityY = float.NaN;
        packet.Payload.AngularVelocityZ = float.NaN;
        return packet;
    }

    public void VerifyPacket(byte[] frame, InjectorOptions options)
    {
        var decoded = new GimbalManagerSetAttitudePacket();
        ReadOnlySpan<byte> readSpan = frame;
        decoded.Deserialize(ref readSpan);
        if (decoded.SystemId != options.SourceSystem ||
            decoded.ComponentId != options.SourceComponent ||
            decoded.Payload.TargetSystem != options.TargetSystem ||
            decoded.Payload.TargetComponent != options.TargetComponent ||
            decoded.Payload.GimbalDeviceId != 1)
        {
            throw new InvalidOperationException("Encoded GIMBAL_MANAGER_SET_ATTITUDE did not match the requested command.");
        }
    }

    public IReadOnlyList<ExpectedResponse> ExpectedResponses(InjectorOptions options) => [];

    public void PrintHelp()
    {
        Console.WriteLine($"Usage: mavlink-injector {name} [options]");
        Console.WriteLine();
        InjectorOptions.PrintHelp(DefaultTargetComponent);
        if (!center) Console.WriteLine("  --roll <degrees>           Requested roll (default: 0)");
        if (!center) Console.WriteLine("  --pitch <degrees>          Requested pitch (default: 0)");
        if (!center) Console.WriteLine("  --yaw <degrees>            Requested yaw (default: 0)");
    }

    public void PrintSent(InjectorOptions options)
    {
        var (roll, pitch, yaw) = center ? (0f, 0f, 0f) :
            (options.RollDegrees, options.PitchDegrees, options.YawDegrees);
        Console.WriteLine("MAVLink Injector");
        Console.WriteLine($"Destination: {options.Host}:{options.Port}");
        Console.WriteLine($"Source:      {options.SourceSystem}/{options.SourceComponent}");
        Console.WriteLine($"Target:      {options.TargetSystem}/{options.TargetComponent}");
        Console.WriteLine("Message:     GIMBAL_MANAGER_SET_ATTITUDE");
        Console.WriteLine($"Requested:   roll={roll:F1}°, pitch={pitch:F1}°, yaw={yaw:F1}°");
        Console.WriteLine("Sent successfully.");
    }

    private static (float W, float X, float Y, float Z) EulerDegreesToQuaternion(float rollDegrees, float pitchDegrees, float yawDegrees)
    {
        var roll = rollDegrees * MathF.PI / 180f;
        var pitch = pitchDegrees * MathF.PI / 180f;
        var yaw = yawDegrees * MathF.PI / 180f;
        var cr = MathF.Cos(roll / 2f); var sr = MathF.Sin(roll / 2f);
        var cp = MathF.Cos(pitch / 2f); var sp = MathF.Sin(pitch / 2f);
        var cy = MathF.Cos(yaw / 2f); var sy = MathF.Sin(yaw / 2f);
        return (cr * cp * cy + sr * sp * sy, sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy, cr * cp * sy - sr * sp * cy);
    }
}

internal sealed record InjectorOptions(IPAddress Host, int Port, byte SourceSystem, byte SourceComponent, byte TargetSystem, byte TargetComponent, float RollDegrees, float PitchDegrees, float YawDegrees)
{
    public static bool TryParse(string[] arguments, byte defaultTargetComponent, out InjectorOptions options, out string error)
    {
        var host = IPAddress.Loopback;
        var port = 14552;
        byte sourceSystem = 255, sourceComponent = 190, targetSystem = 1, targetComponent = defaultTargetComponent;
        var rollDegrees = 0f; var pitchDegrees = 0f; var yawDegrees = 0f;

        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length)
            {
                options = default!;
                error = $"Missing value for {arguments[index]}.";
                return false;
            }

            var option = arguments[index];
            var value = arguments[index + 1];
            switch (option)
            {
                case "--host" when IPAddress.TryParse(value, out var parsedHost): host = parsedHost; break;
                case "--port" when int.TryParse(value, out var parsedPort) && parsedPort is > 0 and <= 65535: port = parsedPort; break;
                case "--source-system" when byte.TryParse(value, out var parsedSourceSystem): sourceSystem = parsedSourceSystem; break;
                case "--source-component" when byte.TryParse(value, out var parsedSourceComponent): sourceComponent = parsedSourceComponent; break;
                case "--target-system" when byte.TryParse(value, out var parsedTargetSystem): targetSystem = parsedTargetSystem; break;
                case "--target-component" when byte.TryParse(value, out var parsedTargetComponent): targetComponent = parsedTargetComponent; break;
                case "--roll" when TryParseFiniteFloat(value, out var parsedRoll): rollDegrees = parsedRoll; break;
                case "--pitch" when TryParseFiniteFloat(value, out var parsedPitch): pitchDegrees = parsedPitch; break;
                case "--yaw" when TryParseFiniteFloat(value, out var parsedYaw): yawDegrees = parsedYaw; break;
                default:
                    options = default!;
                    error = $"Invalid value '{value}' for option '{option}'.";
                    return false;
            }
        }

        options = new InjectorOptions(host, port, sourceSystem, sourceComponent, targetSystem, targetComponent, rollDegrees, pitchDegrees, yawDegrees);
        error = string.Empty;
        return true;
    }

    private static bool TryParseFiniteFloat(string value, out float result) =>
        float.TryParse(value, CultureInfo.InvariantCulture, out result) && float.IsFinite(result);

    public static void PrintHelp(byte defaultTargetComponent)
    {
        Console.WriteLine("  --host <IP address>        Destination host (default: 127.0.0.1)");
        Console.WriteLine("  --port <1-65535>           Destination UDP port (default: 14552)");
        Console.WriteLine("  --source-system <0-255>    Source system ID (default: 255)");
        Console.WriteLine("  --source-component <0-255> Source component ID (default: 190)");
        Console.WriteLine("  --target-system <0-255>    Target system ID (default: 1)");
        Console.WriteLine($"  --target-component <0-255> Target component ID (default: {defaultTargetComponent})");
    }
}

internal static class MavlinkEncoder
{
    public static byte[] Encode(MavlinkV2Message packet)
    {
        var buffer = new byte[MavlinkV2Protocol.PacketV2MaxSize];
        var writeSpan = buffer.AsSpan();
        packet.Serialize(ref writeSpan);
        return buffer[..(buffer.Length - writeSpan.Length)];
    }
}
