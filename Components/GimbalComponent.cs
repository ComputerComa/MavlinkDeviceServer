using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Mavlink;

namespace MavlinkDeviceServer.Components;

public sealed class GimbalComponent(byte systemId, byte componentId)
    : MavlinkComponentBase(systemId, componentId)
{
    private const uint CommandLongMessageId = 76;
    private const uint GimbalDeviceInformationMessageId = 283;
    private const uint GimbalDeviceAttitudeStatusMessageId = 285;

    public override IReadOnlyCollection<uint> HandledMessageIds { get; } =
        [CommandLongMessageId];

    public override IEnumerable<OutgoingMessage> HandleMessage(
        MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD)
        {
            return [];
        }

        CommandLongPacket command;

        try
        {
            command = new CommandLongPacket();
            var readSpan = context.Frame.Span;
            command.Deserialize(ref readSpan);
        }
        catch (Exception exception)
        {
            context.Log.Write($"Gimbal COMMAND_LONG decode failed: {exception}");
            return [];
        }

        if (command.Payload.Command != MavCmd.MavCmdRequestMessage)
        {
            return [Unsupported(command)];
        }

        var requestedMessageId = (uint)Math.Max(0, command.Payload.Param1);
        return requestedMessageId switch
        {
            GimbalDeviceInformationMessageId =>
            [
                new(CreateDeviceInformation(), "GIMBAL_DEVICE_INFORMATION requested response"),
                Accepted(command)
            ],
            GimbalDeviceAttitudeStatusMessageId =>
            [
                new(CreateAttitudeStatus(), "GIMBAL_DEVICE_ATTITUDE_STATUS requested response"),
                Accepted(command)
            ],
            _ => [Unsupported(command)]
        };
    }

    public override IEnumerable<OutgoingMessage> GetPeriodicMessages(
        DateTimeOffset now) =>
        [new(CreateAttitudeStatus(), "GIMBAL_DEVICE_ATTITUDE_STATUS")];

    private GimbalDeviceInformationPacket CreateDeviceInformation()
    {
        var packet = new GimbalDeviceInformationPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.TimeBootMs = BootTimeMilliseconds();
        packet.Payload.Uid = 0x4D_44_53_47_49_4D_42_4CUL;
        packet.Payload.FirmwareVersion = 0x01000000;
        packet.Payload.HardwareVersion = 1;
        packet.Payload.RollMin = -0.7854f;
        packet.Payload.RollMax = 0.7854f;
        packet.Payload.PitchMin = -1.5708f;
        packet.Payload.PitchMax = 0.5236f;
        packet.Payload.YawMin = -3.1416f;
        packet.Payload.YawMax = 3.1416f;
        CreateFixedName("MavlinkDeviceServer").CopyTo(packet.Payload.VendorName, 0);
        CreateFixedName("Fake Gimbal").CopyTo(packet.Payload.ModelName, 0);
        CreateFixedName("Fake MAVLink Gimbal").CopyTo(packet.Payload.CustomName, 0);
        packet.Payload.GimbalDeviceId = ComponentId;
        return packet;
    }

    private GimbalDeviceAttitudeStatusPacket CreateAttitudeStatus()
    {
        var packet = new GimbalDeviceAttitudeStatusPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.TimeBootMs = BootTimeMilliseconds();
        packet.Payload.Q[0] = 1f;
        packet.Payload.GimbalDeviceId = ComponentId;
        return packet;
    }

    private static uint BootTimeMilliseconds() =>
        unchecked((uint)Environment.TickCount64);

    private static char[] CreateFixedName(string value)
    {
        var result = new char[32];
        value.AsSpan(0, Math.Min(value.Length, result.Length)).CopyTo(result);
        return result;
    }

    private OutgoingMessage Accepted(CommandLongPacket command) =>
        new(CreateCommandAck(command, MavResult.MavResultAccepted),
            $"COMMAND_ACK {command.Payload.Command} {MavResult.MavResultAccepted}");

    private OutgoingMessage Unsupported(CommandLongPacket command) =>
        new(CreateCommandAck(command, MavResult.MavResultUnsupported),
            $"COMMAND_ACK {command.Payload.Command} {MavResult.MavResultUnsupported}");

    private CommandAckPacket CreateCommandAck(
        CommandLongPacket command,
        MavResult result)
    {
        var packet = new CommandAckPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.Command = command.Payload.Command;
        packet.Payload.Result = result;
        packet.Payload.TargetSystem = command.SystemId;
        packet.Payload.TargetComponent = command.ComponentId;
        return packet;
    }
}
