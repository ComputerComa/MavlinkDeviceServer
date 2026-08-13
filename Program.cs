using Asv.Mavlink;
using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;

internal class Program
{
    private static void Main(string[] args)
    {
        const byte systemId = 1;
        const byte componentId = (byte)MavComponent.MavCompIdOnboardComputer;

        var outgoing = new HeartbeatPacket
        {
            SystemId = systemId,
            ComponentId = componentId,
            Sequence = 0
        };

        outgoing.Payload.Type = MavType.MavTypeOnboardController;
        outgoing.Payload.Autopilot = MavAutopilot.MavAutopilotInvalid;
        outgoing.Payload.BaseMode = 0;
        outgoing.Payload.CustomMode = 0;
        outgoing.Payload.SystemStatus = MavState.MavStateActive;
        outgoing.Payload.MavlinkVersion = 3;

        // Allocate enough space for any MAVLink 2 packet.
        var buffer = new byte[MavlinkV2Protocol.PacketV2MaxSize];
        var writeSpan = buffer.AsSpan();

        // Serialize advances writeSpan past the encoded bytes.
        outgoing.Serialize(ref writeSpan);
        var encodedLength = buffer.Length - writeSpan.Length;

        Console.WriteLine($"Encoded heartbeat: {encodedLength} bytes");
        Console.WriteLine($"Frame: {Convert.ToHexString(buffer.AsSpan(0, encodedLength))}");

        // Decode the frame into a fresh packet.
        var incoming = new HeartbeatPacket();
        ReadOnlySpan<byte> readSpan = buffer.AsSpan(0, encodedLength);
        incoming.Deserialize(ref readSpan);

        Console.WriteLine();
        Console.WriteLine("Decoded heartbeat:");
        Console.WriteLine($"  Message:      {incoming.Name} ({incoming.Id})");
        Console.WriteLine($"  System ID:    {incoming.SystemId}");
        Console.WriteLine($"  Component ID: {incoming.ComponentId}");
        Console.WriteLine($"  Type:         {incoming.Payload.Type}");
        Console.WriteLine($"  Autopilot:    {incoming.Payload.Autopilot}");
        Console.WriteLine($"  Status:       {incoming.Payload.SystemStatus}");
        Console.WriteLine($"  MAVLink ver:  {incoming.Payload.MavlinkVersion}");

        // Fail loudly if anything changed during the round trip.
        if (incoming.SystemId != systemId ||
            incoming.ComponentId != componentId ||
            incoming.Payload.Type != MavType.MavTypeOnboardController ||
            incoming.Payload.Autopilot != MavAutopilot.MavAutopilotInvalid ||
            incoming.Payload.SystemStatus != MavState.MavStateActive ||
            incoming.Payload.MavlinkVersion != 3)
        {
            throw new InvalidOperationException("Heartbeat round-trip validation failed.");
        }

        Console.WriteLine();
        Console.WriteLine("Heartbeat round-trip succeeded.");


        Console.WriteLine();
        Console.WriteLine("Testing ONBOARD_COMPUTER_STATUS...");

        var outgoingStatus = new OnboardComputerStatusPacket
        {
            SystemId = systemId,
            ComponentId = componentId,
            Sequence = 1
        };

        // Known test values. We will replace these with real Linux measurements later.
        outgoingStatus.Payload.TimeUsec = 123_456_789;
        outgoingStatus.Payload.Uptime = 42_000;
        outgoingStatus.Payload.RamTotal = 8_192;
        outgoingStatus.Payload.RamUsage = 2_048;

        var statusBuffer = new byte[MavlinkV2Protocol.PacketV2MaxSize];
        var statusWriteSpan = statusBuffer.AsSpan();

        outgoingStatus.Serialize(ref statusWriteSpan);
        var statusLength = statusBuffer.Length - statusWriteSpan.Length;

        Console.WriteLine($"Encoded status: {statusLength} bytes");
        Console.WriteLine(
            $"Frame: {Convert.ToHexString(statusBuffer.AsSpan(0, statusLength))}");

        var incomingStatus = new OnboardComputerStatusPacket();
        ReadOnlySpan<byte> statusReadSpan =
            statusBuffer.AsSpan(0, statusLength);

        incomingStatus.Deserialize(ref statusReadSpan);

        Console.WriteLine();
        Console.WriteLine("Decoded onboard-computer status:");
        Console.WriteLine($"  Message:      {incomingStatus.Name} ({incomingStatus.Id})");
        Console.WriteLine($"  System ID:    {incomingStatus.SystemId}");
        Console.WriteLine($"  Component ID: {incomingStatus.ComponentId}");
        Console.WriteLine($"  Time usec:    {incomingStatus.Payload.TimeUsec}");
        Console.WriteLine($"  Uptime:       {incomingStatus.Payload.Uptime} ms");
        Console.WriteLine($"  RAM total:    {incomingStatus.Payload.RamTotal} MiB");
        Console.WriteLine($"  RAM usage:    {incomingStatus.Payload.RamUsage} MiB");

        if (incomingStatus.SystemId != systemId ||
            incomingStatus.ComponentId != componentId ||
            incomingStatus.Payload.TimeUsec != 123_456_789 ||
            incomingStatus.Payload.Uptime != 42_000 ||
            incomingStatus.Payload.RamTotal != 8_192 ||
            incomingStatus.Payload.RamUsage != 2_048)
        {
            throw new InvalidOperationException(
                "ONBOARD_COMPUTER_STATUS round-trip validation failed.");
        }

        Console.WriteLine();
        Console.WriteLine("ONBOARD_COMPUTER_STATUS round-trip succeeded.");



        Console.WriteLine();
        Console.WriteLine("Checking camera/gimbal dialect coverage...");

        MavlinkV2Message[] requiredMessages =
        [
            new CameraInformationPacket(),
    new CameraSettingsPacket(),
    new CameraCaptureStatusPacket(),
    new VideoStreamInformationPacket(),
    new GimbalDeviceInformationPacket(),
    new GimbalDeviceSetAttitudePacket(),
    new GimbalDeviceAttitudeStatusPacket(),
    new CommandLongPacket(),
    new CommandAckPacket()
        ];

        foreach (var message in requiredMessages)
        {
            Console.WriteLine($"  {message.Name,-36} ID {message.Id}");
        }

        Console.WriteLine();
        Console.WriteLine("Required camera/gimbal message types are available.");

    }
}