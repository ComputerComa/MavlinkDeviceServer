using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;

public sealed class OnboardComputerComponent(byte systemId, byte componentId) : MavlinkComponentBase(systemId, componentId)
{
    private const uint CommandLongMessageId = 76, OnboardComputerStatusMessageId = 390; private int _statusCounter;
    public override IReadOnlyCollection<uint> HandledMessageIds { get; } = [CommandLongMessageId];
    public override IEnumerable<OutgoingMessage> HandleMessage(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) return [];
        CommandLongPacket command; try { command = new(); var readSpan = context.Frame.Span; command.Deserialize(ref readSpan); } catch (Exception exception) { Console.WriteLine($"Failed to decode COMMAND_LONG: {exception.Message}"); context.Log.Write($"COMMAND_LONG decode failed: {exception}"); return []; }
        if (command.Payload.Command == MavCmd.MavCmdRequestMessage) return HandleRequestMessage(command, context.Log);
        Console.WriteLine("  Result: Unsupported command"); Console.WriteLine(); context.Log.Write($"COMMAND_LONG unsupported: {command.Payload.Command}");
        return [new(CreateCommandAck(command, MavResult.MavResultUnsupported), $"COMMAND_ACK {command.Payload.Command} {MavResult.MavResultUnsupported}")];
    }
    public override IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now)
    {
        var messages = new List<OutgoingMessage> { new(CreateHeartbeat(), $"HEARTBEAT SYS={SystemId} COMP={ComponentId}") };
        if (++_statusCounter >= 5) { _statusCounter = 0; messages.Add(new(CreateOnboardComputerStatus(), "ONBOARD_COMPUTER_STATUS")); }
        return messages;
    }
    private IEnumerable<OutgoingMessage> HandleRequestMessage(CommandLongPacket command, Logging.DebugLog log)
    {
        var requestedMessageId = (uint)Math.Max(0, command.Payload.Param1); Console.WriteLine($"  Requested message ID: {requestedMessageId}");
        if (requestedMessageId == OnboardComputerStatusMessageId) { Console.WriteLine("  Result: ONBOARD_COMPUTER_STATUS sent"); Console.WriteLine("  ACK:    Accepted"); Console.WriteLine(); log.Write("MAV_CMD_REQUEST_MESSAGE accepted for ONBOARD_COMPUTER_STATUS"); return [new(CreateOnboardComputerStatus(), "ONBOARD_COMPUTER_STATUS requested response"), new(CreateCommandAck(command, MavResult.MavResultAccepted), $"COMMAND_ACK {command.Payload.Command} {MavResult.MavResultAccepted}")]; }
        Console.WriteLine($"  Result: Message {requestedMessageId} unsupported"); Console.WriteLine("  ACK:    Unsupported"); Console.WriteLine(); log.Write($"Requested message {requestedMessageId} unsupported"); return [new(CreateCommandAck(command, MavResult.MavResultUnsupported), $"COMMAND_ACK {command.Payload.Command} {MavResult.MavResultUnsupported}")];
    }
    private HeartbeatPacket CreateHeartbeat() { var packet = new HeartbeatPacket { SystemId = SystemId, ComponentId = ComponentId, Sequence = NextSequence() }; packet.Payload.Type = MavType.MavTypeOnboardController; packet.Payload.Autopilot = MavAutopilot.MavAutopilotInvalid; packet.Payload.BaseMode = 0; packet.Payload.CustomMode = 0; packet.Payload.SystemStatus = MavState.MavStateActive; packet.Payload.MavlinkVersion = 3; return packet; }
    private OnboardComputerStatusPacket CreateOnboardComputerStatus() { var packet = new OnboardComputerStatusPacket { SystemId = SystemId, ComponentId = ComponentId, Sequence = NextSequence() }; packet.Payload.TimeUsec = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000UL; packet.Payload.Uptime = unchecked((uint)Math.Min(Environment.TickCount64, uint.MaxValue)); packet.Payload.RamTotal = 0; packet.Payload.RamUsage = 0; return packet; }
    private CommandAckPacket CreateCommandAck(CommandLongPacket command, MavResult result) { var packet = new CommandAckPacket { SystemId = SystemId, ComponentId = ComponentId, Sequence = NextSequence() }; packet.Payload.Command = command.Payload.Command; packet.Payload.Result = result; packet.Payload.Progress = 0; packet.Payload.ResultParam2 = 0; packet.Payload.TargetSystem = command.SystemId; packet.Payload.TargetComponent = command.ComponentId; return packet; }
}
