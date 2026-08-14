using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkRouter(byte deviceSystemId, ComponentRegistry components, DebugLog log)
{
    public IEnumerable<OutgoingMessage> Route(MavlinkMessageContext context) => context.MessageId switch { 0 => ProcessHeartbeat(context), 76 => ProcessCommandLong(context), _ => [] };
    public IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now) => components.Components.SelectMany(x => x.GetPeriodicMessages(now));
    private IEnumerable<OutgoingMessage> ProcessHeartbeat(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) { log.Write("MAVLink 1 HEARTBEAT received; this prototype decodes MAVLink 2 only"); return []; }
        try { var packet = new HeartbeatPacket(); var readSpan = context.Frame.Span; packet.Deserialize(ref readSpan); log.Write($"HEARTBEAT decoded SYS={packet.SystemId} COMP={packet.ComponentId} TYPE={packet.Payload.Type} AUTOPILOT={packet.Payload.Autopilot} STATE={packet.Payload.SystemStatus} BASE_MODE={packet.Payload.BaseMode}"); } catch (Exception exception) { Console.WriteLine($"Failed to decode HEARTBEAT: {exception.Message}"); log.Write($"HEARTBEAT decode failed: {exception}"); }
        return [];
    }
    private IEnumerable<OutgoingMessage> ProcessCommandLong(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) { log.Write("MAVLink 1 COMMAND_LONG received but ignored"); return []; }
        CommandLongPacket command; try { command = new(); var readSpan = context.Frame.Span; command.Deserialize(ref readSpan); } catch (Exception exception) { Console.WriteLine($"Failed to decode COMMAND_LONG: {exception.Message}"); log.Write($"COMMAND_LONG decode failed: {exception}"); return []; }
        Console.WriteLine($"COMMAND_LONG received from {command.SystemId}/{command.ComponentId}"); Console.WriteLine($"  Command: {command.Payload.Command}"); Console.WriteLine($"  Target:  {command.Payload.TargetSystem}/{command.Payload.TargetComponent}"); Console.WriteLine($"  Param 1: {command.Payload.Param1}");
        log.Write($"COMMAND_LONG decoded SOURCE={command.SystemId}/{command.ComponentId} TARGET={command.Payload.TargetSystem}/{command.Payload.TargetComponent} COMMAND={command.Payload.Command} CONFIRMATION={command.Payload.Confirmation} PARAMS=[{command.Payload.Param1}, {command.Payload.Param2}, {command.Payload.Param3}, {command.Payload.Param4}, {command.Payload.Param5}, {command.Payload.Param6}, {command.Payload.Param7}]");
        if (command.Payload.TargetSystem != 0 && command.Payload.TargetSystem != deviceSystemId) return Ignore();
        var recipients = components.GetMessageRecipients(context.MessageId, command.Payload.TargetSystem, command.Payload.TargetComponent).ToList();
        return recipients.Count == 0 ? Ignore() : recipients.SelectMany(x => x.HandleMessage(context)).ToList();
    }
    private IEnumerable<OutgoingMessage> Ignore() { Console.WriteLine("  Ignored: command targets another component."); Console.WriteLine(); log.Write("COMMAND_LONG ignored because target does not match"); return []; }
}
