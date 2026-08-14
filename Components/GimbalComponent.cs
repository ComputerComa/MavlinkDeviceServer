using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Gimbal;
using MavlinkDeviceServer.Mavlink;

namespace MavlinkDeviceServer.Components;

public sealed class GimbalComponent(byte systemId, byte componentId, IGimbalDevice gimbal)
    : MavlinkComponentBase(systemId, componentId)
{
    private const uint CommandLongMessageId = 76;
    private const uint GimbalManagerInformationMessageId = 280;
    private const uint GimbalManagerSetAttitudeMessageId = 282;
    private const uint GimbalDeviceInformationMessageId = 283;
    private const uint GimbalDeviceSetAttitudeMessageId = 284;
    private const uint GimbalDeviceAttitudeStatusMessageId = 285;
    private const uint AutopilotStateForGimbalDeviceMessageId = 286;
    private const byte GimbalDeviceComponentId = 154;

    public override IReadOnlyCollection<uint> HandledMessageIds { get; } =
        [CommandLongMessageId, GimbalManagerSetAttitudeMessageId, GimbalDeviceSetAttitudeMessageId,
            AutopilotStateForGimbalDeviceMessageId];
    public override IReadOnlyCollection<uint> HandledRequestMessageIds { get; } =
        [GimbalManagerInformationMessageId, GimbalDeviceInformationMessageId,
            GimbalDeviceAttitudeStatusMessageId];

    public override IEnumerable<OutgoingMessage> HandleMessage(
        MavlinkMessageContext context)
    {
        return context.MessageId switch
        {
            CommandLongMessageId => HandleCommandLong(context),
            GimbalManagerSetAttitudeMessageId => HandleManagerSetAttitude(context),
            GimbalDeviceSetAttitudeMessageId => HandleDeviceSetAttitude(context),
            AutopilotStateForGimbalDeviceMessageId => HandleAutopilotState(context),
            _ => []
        };
    }

    private IEnumerable<OutgoingMessage> HandleCommandLong(MavlinkMessageContext context)
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
            GimbalManagerInformationMessageId =>
            [
                new(CreateManagerInformation(), "GIMBAL_MANAGER_INFORMATION requested response"),
                Accepted(command)
            ],
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

    private IEnumerable<OutgoingMessage> HandleManagerSetAttitude(
        MavlinkMessageContext context)
    {
        GimbalManagerSetAttitudePacket command;
        try
        {
            command = new GimbalManagerSetAttitudePacket();
            var readSpan = context.Frame.Span;
            command.Deserialize(ref readSpan);
        }
        catch (Exception exception)
        {
            context.Log.Write($"GIMBAL_MANAGER_SET_ATTITUDE decode failed: {exception}");
            return [];
        }

        if (command.Payload.GimbalDeviceId is not 0 and not GimbalDeviceComponentId)
        {
            return [];
        }

        var attitude = new GimbalQuaternion(
            command.Payload.Q[0], command.Payload.Q[1],
            command.Payload.Q[2], command.Payload.Q[3]);

        if (!gimbal.SetAttitude(
                attitude,
                command.Payload.AngularVelocityX,
                command.Payload.AngularVelocityY,
                command.Payload.AngularVelocityZ))
        {
            context.Log.Write("GIMBAL_MANAGER_SET_ATTITUDE ignored invalid quaternion");
        }

        return [];
    }

    private IEnumerable<OutgoingMessage> HandleDeviceSetAttitude(
        MavlinkMessageContext context)
    {
        GimbalDeviceSetAttitudePacket command;
        try
        {
            command = new GimbalDeviceSetAttitudePacket();
            var readSpan = context.Frame.Span;
            command.Deserialize(ref readSpan);
        }
        catch (Exception exception)
        {
            context.Log.Write($"GIMBAL_DEVICE_SET_ATTITUDE decode failed: {exception}");
            return [];
        }

        // Device setpoints control one MAVLink gimbal component; do not consume broadcast setpoints.
        if (command.Payload.TargetSystem != SystemId || command.Payload.TargetComponent != ComponentId)
        {
            return [];
        }

        if (!TryGetYawFrame(command.Payload.Flags, out var yawFrame, out var unsupportedFlags))
        {
            context.Log.Write($"GIMBAL_DEVICE_SET_ATTITUDE rejected unsupported flags: {unsupportedFlags}");
            return [];
        }

        var quaternion = new GimbalQuaternion(
            command.Payload.Q[0], command.Payload.Q[1],
            command.Payload.Q[2], command.Payload.Q[3]);

        var allQuaternionValuesAreNaN =
            float.IsNaN(quaternion.W) && float.IsNaN(quaternion.X) &&
            float.IsNaN(quaternion.Y) && float.IsNaN(quaternion.Z);

        if (allQuaternionValuesAreNaN)
        {
            gimbal.SetRates(
                command.Payload.AngularVelocityX,
                command.Payload.AngularVelocityY,
                command.Payload.AngularVelocityZ);
        }
        else if (!gimbal.SetAttitude(
                     quaternion,
                     command.Payload.AngularVelocityX,
                     command.Payload.AngularVelocityY,
                     command.Payload.AngularVelocityZ))
        {
            context.Log.Write("GIMBAL_DEVICE_SET_ATTITUDE rejected an invalid quaternion");
            return [];
        }

        gimbal.SetYawFrame(yawFrame);
        LogDeviceSetAttitude(context, command, allQuaternionValuesAreNaN);
        return [];
    }

    private IEnumerable<OutgoingMessage> HandleAutopilotState(MavlinkMessageContext context)
    {
        AutopilotStateForGimbalDevicePacket packet;
        try
        {
            packet = new AutopilotStateForGimbalDevicePacket();
            var readSpan = context.Frame.Span;
            packet.Deserialize(ref readSpan);
        }
        catch (Exception exception)
        {
            context.Log.Write($"AUTOPILOT_STATE_FOR_GIMBAL_DEVICE decode failed: {exception}");
            return [];
        }

        if (packet.Payload.TargetSystem != SystemId || packet.Payload.TargetComponent != ComponentId)
        {
            return [];
        }

        gimbal.SetAutopilotState(new GimbalAutopilotState(
            packet.Payload.TimeBootUs,
            new GimbalQuaternion(packet.Payload.Q[0], packet.Payload.Q[1], packet.Payload.Q[2], packet.Payload.Q[3]),
            packet.Payload.AngularVelocityZ,
            packet.Payload.FeedForwardAngularVelocityZ));
        context.Log.Write(
            $"AUTOPILOT_STATE_FOR_GIMBAL_DEVICE received from {context.Source.SystemId}/{context.Source.ComponentId} " +
            $"for {packet.Payload.TargetSystem}/{packet.Payload.TargetComponent}");
        return [];
    }

    public override IEnumerable<OutgoingMessage> GetPeriodicMessages(
        DateTimeOffset now) =>
        [
            new(CreateHeartbeat(), $"HEARTBEAT SYS={SystemId} COMP={ComponentId}"),
            new(CreateManagerStatus(), "GIMBAL_MANAGER_STATUS"),
            new(CreateAttitudeStatus(), "GIMBAL_DEVICE_ATTITUDE_STATUS")
        ];

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
        packet.Payload.FirmwareVersion = PackVersion(1, 0, 0, 0);
        packet.Payload.HardwareVersion = 1;
        packet.Payload.CapFlags =
            GimbalDeviceCapFlags.GimbalDeviceCapFlagsHasRollAxis |
            GimbalDeviceCapFlags.GimbalDeviceCapFlagsHasPitchAxis |
            GimbalDeviceCapFlags.GimbalDeviceCapFlagsHasYawAxis;
        packet.Payload.RollMin = gimbal.Limits.RollMinRadians;
        packet.Payload.RollMax = gimbal.Limits.RollMaxRadians;
        packet.Payload.PitchMin = gimbal.Limits.PitchMinRadians;
        packet.Payload.PitchMax = gimbal.Limits.PitchMaxRadians;
        packet.Payload.YawMin = gimbal.Limits.YawMinRadians;
        packet.Payload.YawMax = gimbal.Limits.YawMaxRadians;
        CreateFixedName("MavlinkDeviceServer").CopyTo(packet.Payload.VendorName, 0);
        CreateFixedName("Fake Gimbal").CopyTo(packet.Payload.ModelName, 0);
        CreateFixedName("Fake MAVLink Gimbal").CopyTo(packet.Payload.CustomName, 0);
        // This MAVLink device is managed by the separate ArduPilot component.
        packet.Payload.GimbalDeviceId = 0;
        return packet;
    }

    private GimbalManagerInformationPacket CreateManagerInformation()
    {
        var packet = new GimbalManagerInformationPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.TimeBootMs = BootTimeMilliseconds();
        packet.Payload.CapFlags =
            GimbalManagerCapFlags.GimbalManagerCapFlagsHasRollAxis |
            GimbalManagerCapFlags.GimbalManagerCapFlagsHasPitchAxis |
            GimbalManagerCapFlags.GimbalManagerCapFlagsHasYawAxis;
        packet.Payload.GimbalDeviceId = GimbalDeviceComponentId;
        packet.Payload.RollMin = gimbal.Limits.RollMinRadians;
        packet.Payload.RollMax = gimbal.Limits.RollMaxRadians;
        packet.Payload.PitchMin = gimbal.Limits.PitchMinRadians;
        packet.Payload.PitchMax = gimbal.Limits.PitchMaxRadians;
        packet.Payload.YawMin = gimbal.Limits.YawMinRadians;
        packet.Payload.YawMax = gimbal.Limits.YawMaxRadians;
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

        var state = gimbal.State;
        var attitude = GimbalQuaternion.FromEuler(
            state.RollRadians,
            state.PitchRadians,
            state.YawRadians);

        packet.Payload.TimeBootMs = BootTimeMilliseconds();
        packet.Payload.Q[0] = attitude.W;
        packet.Payload.Q[1] = attitude.X;
        packet.Payload.Q[2] = attitude.Y;
        packet.Payload.Q[3] = attitude.Z;
        packet.Payload.AngularVelocityX = state.RollRateRadiansPerSecond;
        packet.Payload.AngularVelocityY = state.PitchRateRadiansPerSecond;
        packet.Payload.AngularVelocityZ = state.YawRateRadiansPerSecond;
        packet.Payload.Flags = state.YawFrame == GimbalYawFrame.Earth
            ? GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame
            : GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame;
        packet.Payload.GimbalDeviceId = 0;
        return packet;
    }

    private HeartbeatPacket CreateHeartbeat()
    {
        var packet = new HeartbeatPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.Type = MavType.MavTypeGimbal;
        packet.Payload.Autopilot = MavAutopilot.MavAutopilotInvalid;
        packet.Payload.SystemStatus = MavState.MavStateActive;
        packet.Payload.MavlinkVersion = 3;
        return packet;
    }

    private GimbalManagerStatusPacket CreateManagerStatus()
    {
        var packet = new GimbalManagerStatusPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.TimeBootMs = BootTimeMilliseconds();
        packet.Payload.GimbalDeviceId = GimbalDeviceComponentId;
        return packet;
    }

    private static bool TryGetYawFrame(
        GimbalDeviceFlags flags,
        out GimbalYawFrame yawFrame,
        out GimbalDeviceFlags unsupportedFlags)
    {
        var yawInVehicleFrame = flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame);
        var yawInEarthFrame = flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame);
        var yawLock = flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawLock);
        var supportedFlags =
            GimbalDeviceFlags.GimbalDeviceFlagsRollLock |
            GimbalDeviceFlags.GimbalDeviceFlagsPitchLock |
            GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame |
            GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame |
            GimbalDeviceFlags.GimbalDeviceFlagsYawLock;

        unsupportedFlags = flags & ~supportedFlags;
        if (yawInVehicleFrame && yawInEarthFrame)
        {
            unsupportedFlags |= GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame |
                                GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame;
            yawFrame = GimbalYawFrame.Vehicle;
            return false;
        }

        if (unsupportedFlags != 0)
        {
            yawFrame = GimbalYawFrame.Vehicle;
            return false;
        }

        yawFrame = yawInEarthFrame || (!yawInVehicleFrame && yawLock)
            ? GimbalYawFrame.Earth
            : GimbalYawFrame.Vehicle;
        return true;
    }

    private static void LogDeviceSetAttitude(
        MavlinkMessageContext context,
        GimbalDeviceSetAttitudePacket command,
        bool rateOnly)
    {
        var message =
            $"GIMBAL_DEVICE_SET_ATTITUDE from {context.Source.SystemId}/{context.Source.ComponentId}\n" +
            $"  Target: {command.Payload.TargetSystem}/{command.Payload.TargetComponent}\n" +
            $"  Flags: {DescribeDeviceFlags(command.Payload.Flags)}\n" +
            $"  Mode: {(rateOnly ? "rate" : "attitude")}\n" +
            $"  Quaternion: [{command.Payload.Q[0]}, {command.Payload.Q[1]}, {command.Payload.Q[2]}, {command.Payload.Q[3]}]" +
            (rateOnly ? " (rate-only)" : string.Empty) + "\n" +
            $"  Angular velocity: [{command.Payload.AngularVelocityX}, {command.Payload.AngularVelocityY}, {command.Payload.AngularVelocityZ}]" +
            GetEulerDescription(command, rateOnly);
        Console.WriteLine(message);
        context.Log.Write(message.Replace(Environment.NewLine, " | "));
    }

    private static string GetEulerDescription(GimbalDeviceSetAttitudePacket command, bool rateOnly)
    {
        if (rateOnly)
        {
            return string.Empty;
        }

        var quaternion = new GimbalQuaternion(
            command.Payload.Q[0], command.Payload.Q[1], command.Payload.Q[2], command.Payload.Q[3]);
        return !quaternion.TryToEuler(out var roll, out var pitch, out var yaw)
            ? string.Empty
            : $"\n  Euler: roll={RadiansToDegrees(roll):F1}, pitch={RadiansToDegrees(pitch):F1}, yaw={RadiansToDegrees(yaw):F1} deg";
    }

    private static string DescribeDeviceFlags(GimbalDeviceFlags flags)
    {
        var names = new List<string>();
        if (flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsRollLock)) names.Add("RollLock");
        if (flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsPitchLock)) names.Add("PitchLock");
        if (flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawLock)) names.Add("YawLock");
        if (flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame)) names.Add("YawInVehicleFrame");
        if (flags.HasFlag(GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame)) names.Add("YawInEarthFrame");
        return names.Count == 0 ? "None" : string.Join("|", names);
    }

    private static float RadiansToDegrees(float radians) => radians * 180f / MathF.PI;

    private static uint BootTimeMilliseconds() =>
        unchecked((uint)Environment.TickCount64);

    private static uint PackVersion(byte major, byte minor, byte patch, byte dev) =>
        ((uint)dev << 24) |
        ((uint)patch << 16) |
        ((uint)minor << 8) |
        major;

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
