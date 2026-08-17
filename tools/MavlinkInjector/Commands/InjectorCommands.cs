using Asv.Mavlink;
using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkInjector.Mavlink;

namespace MavlinkInjector.Commands;

public static class InjectorCommands
{
    private const uint GimbalDeviceInformationMessageId = 283;
    private const byte GimbalDeviceComponentId = 154;

    public static async Task<int> RunGimbalInfoAsync(GimbalInfoOptions options)
        => await SendMessageRequestAsync(
            options,
            GimbalDeviceInformationMessageId,
            "GIMBAL_DEVICE_INFORMATION (283)",
            "gimbal-info");

    private static async Task<int> SendMessageRequestAsync(
        CommonInjectorOptions options,
        uint requestedMessageId,
        string requestedMessageName,
        string commandName)
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
        packet.Payload.Param1 = requestedMessageId;

        return await SendAsync(
            packet,
            options,
            (frame, currentOptions) => VerifyMessageRequest(frame, currentOptions, requestedMessageId, commandName),
            "MAV_CMD_REQUEST_MESSAGE",
            requestedMessageName);
    }

    public static Task<int> RunGimbalSetAttitudeAsync(GimbalSetAttitudeOptions options) =>
        !float.IsFinite(options.Roll) || !float.IsFinite(options.Pitch) || !float.IsFinite(options.Yaw)
            ? Task.FromResult(InvalidAngles())
            : SendGimbalAttitudeAsync(options, options.Roll, options.Pitch, options.Yaw);

    public static Task<int> RunGimbalDeviceSetAttitudeAsync(GimbalDeviceSetAttitudeOptions options) =>
        !float.IsFinite(options.Roll) || !float.IsFinite(options.Pitch) || !float.IsFinite(options.Yaw)
            ? Task.FromResult(InvalidAngles())
            : options.EarthFrame && options.VehicleFrame
                ? Task.FromResult(InvalidDeviceFrame())
            : SendGimbalDeviceAttitudeAsync(options, options.Roll, options.Pitch, options.Yaw);

    public static Task<int> RunGimbalCenterAsync(GimbalCenterOptions options) =>
        SendGimbalAttitudeAsync(options, 0f, 0f, 0f);

    private static int InvalidAngles()
    {
        Console.Error.WriteLine("Roll, pitch, and yaw must be finite numeric values.");
        return 2;
    }

    private static int InvalidDeviceFrame()
    {
        Console.Error.WriteLine("Specify either --earth-frame or --vehicle-frame, not both.");
        return 2;
    }

    private static async Task<int> SendGimbalAttitudeAsync(CommonInjectorOptions options, float roll, float pitch, float yaw)
    {
        var quaternion = EulerDegreesToQuaternion(roll, pitch, yaw);
        var packet = new GimbalManagerSetAttitudePacket
        {
            SystemId = options.SourceSystem,
            ComponentId = options.SourceComponent,
            Sequence = 0
        };

        packet.Payload.TargetSystem = options.TargetSystem;
        packet.Payload.TargetComponent = options.TargetComponent;
        packet.Payload.GimbalDeviceId = GimbalDeviceComponentId;
        packet.Payload.Q[0] = quaternion.W;
        packet.Payload.Q[1] = quaternion.X;
        packet.Payload.Q[2] = quaternion.Y;
        packet.Payload.Q[3] = quaternion.Z;
        packet.Payload.AngularVelocityX = float.NaN;
        packet.Payload.AngularVelocityY = float.NaN;
        packet.Payload.AngularVelocityZ = float.NaN;

        return await SendAsync(packet, options, VerifyGimbalAttitude,
            "GIMBAL_MANAGER_SET_ATTITUDE",
            $"roll={roll:F1} deg, pitch={pitch:F1} deg, yaw={yaw:F1} deg");
    }

    private static async Task<int> SendGimbalDeviceAttitudeAsync(GimbalDeviceSetAttitudeOptions options, float roll, float pitch, float yaw)
    {
        var quaternion = EulerDegreesToQuaternion(roll, pitch, yaw);
        var packet = new GimbalDeviceSetAttitudePacket
        {
            SystemId = options.SourceSystem,
            ComponentId = options.SourceComponent,
            Sequence = 0
        };

        packet.Payload.TargetSystem = options.TargetSystem;
        packet.Payload.TargetComponent = options.TargetComponent;
        var flags = CreateDeviceFlags(options);
        packet.Payload.Flags = flags;
        packet.Payload.Q[0] = quaternion.W;
        packet.Payload.Q[1] = quaternion.X;
        packet.Payload.Q[2] = quaternion.Y;
        packet.Payload.Q[3] = quaternion.Z;
        packet.Payload.AngularVelocityX = float.NaN;
        packet.Payload.AngularVelocityY = float.NaN;
        packet.Payload.AngularVelocityZ = float.NaN;

        return await SendAsync(packet, options, (frame, currentOptions) => VerifyGimbalDeviceAttitude(frame, currentOptions, flags),
            "GIMBAL_DEVICE_SET_ATTITUDE",
            $"roll={roll:F1} deg, pitch={pitch:F1} deg, yaw={yaw:F1} deg");
    }

    private static async Task<int> SendAsync(
        MavlinkV2Message packet,
        CommonInjectorOptions options,
        Action<byte[], CommonInjectorOptions> verify,
        string messageName,
        string requested)
    {
        try
        {
            var client = new MavlinkInjectorClient(options.Host, options.Port);
            var frame = client.Serialize(packet);
            verify(frame, options);
            await client.SendAsync(frame);

            Console.WriteLine("MAVLink Injector");
            Console.WriteLine($"Destination: {client.Destination}");
            Console.WriteLine($"Source:      {options.SourceSystem}/{options.SourceComponent}");
            Console.WriteLine($"Target:      {options.TargetSystem}/{options.TargetComponent}");
            Console.WriteLine($"Message:     {messageName}");
            Console.WriteLine($"Requested:   {requested}");
            Console.WriteLine("Sent successfully.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to inject MAVLink command: {exception.Message}");
            return 1;
        }
    }

    private static void VerifyMessageRequest(
        byte[] frame,
        CommonInjectorOptions options,
        uint requestedMessageId,
        string commandName)
    {
        var packet = new CommandLongPacket();
        ReadOnlySpan<byte> readSpan = frame;
        packet.Deserialize(ref readSpan);

        if (packet.SystemId != options.SourceSystem || packet.ComponentId != options.SourceComponent ||
            packet.Payload.TargetSystem != options.TargetSystem || packet.Payload.TargetComponent != options.TargetComponent ||
            packet.Payload.Command != MavCmd.MavCmdRequestMessage || packet.Payload.Confirmation != 0 ||
            packet.Payload.Param1 != requestedMessageId ||
            packet.Payload.Param2 != 0 || packet.Payload.Param3 != 0 || packet.Payload.Param4 != 0 ||
            packet.Payload.Param5 != 0 || packet.Payload.Param6 != 0 || packet.Payload.Param7 != 0)
        {
            throw new InvalidOperationException($"Encoded COMMAND_LONG did not match {commandName}.");
        }
    }

    private static void VerifyGimbalAttitude(byte[] frame, CommonInjectorOptions options)
    {
        var packet = new GimbalManagerSetAttitudePacket();
        ReadOnlySpan<byte> readSpan = frame;
        packet.Deserialize(ref readSpan);

        if (packet.SystemId != options.SourceSystem || packet.ComponentId != options.SourceComponent ||
            packet.Payload.TargetSystem != options.TargetSystem || packet.Payload.TargetComponent != options.TargetComponent ||
            packet.Payload.GimbalDeviceId != GimbalDeviceComponentId)
        {
            throw new InvalidOperationException("Encoded GIMBAL_MANAGER_SET_ATTITUDE did not match the requested command.");
        }
    }

    private static void VerifyGimbalDeviceAttitude(
        byte[] frame,
        CommonInjectorOptions options,
        GimbalDeviceFlags expectedFlags)
    {
        var packet = new GimbalDeviceSetAttitudePacket();
        ReadOnlySpan<byte> readSpan = frame;
        packet.Deserialize(ref readSpan);

        if (packet.SystemId != options.SourceSystem || packet.ComponentId != options.SourceComponent ||
            packet.Payload.TargetSystem != options.TargetSystem || packet.Payload.TargetComponent != options.TargetComponent ||
            packet.Payload.Flags != expectedFlags)
        {
            throw new InvalidOperationException("Encoded GIMBAL_DEVICE_SET_ATTITUDE did not match the requested command.");
        }
    }

    private static GimbalDeviceFlags CreateDeviceFlags(GimbalDeviceSetAttitudeOptions options)
    {
        var flags = options.EarthFrame
            ? GimbalDeviceFlags.GimbalDeviceFlagsYawInEarthFrame
            : GimbalDeviceFlags.GimbalDeviceFlagsYawInVehicleFrame;
        if (options.RollLock) flags |= GimbalDeviceFlags.GimbalDeviceFlagsRollLock;
        if (options.PitchLock) flags |= GimbalDeviceFlags.GimbalDeviceFlagsPitchLock;
        if (options.YawLock) flags |= GimbalDeviceFlags.GimbalDeviceFlagsYawLock;
        return flags;
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
