using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkMessageDispatcher(byte deviceSystemId, ComponentRegistry components, DebugLog log)
{
    public IEnumerable<OutgoingMessage> Route(MavlinkMessageContext context) => context.MessageId switch { 0 => ProcessHeartbeat(context), 76 => ProcessCommandLong(context), 282 => ProcessGimbalManagerSetAttitude(context), 284 => ProcessGimbalDeviceSetAttitude(context), _ => [] };
    public IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now) => components.Components.SelectMany(x => x.GetPeriodicMessages(now));
    private IEnumerable<OutgoingMessage> ProcessHeartbeat(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) return [];
        try { var packet = new HeartbeatPacket(); var readSpan = context.Frame.Span; packet.Deserialize(ref readSpan); log.Write($"HEARTBEAT decoded SYS={packet.SystemId} COMP={packet.ComponentId} TYPE={packet.Payload.Type} AUTOPILOT={packet.Payload.Autopilot} STATE={packet.Payload.SystemStatus} BASE_MODE={packet.Payload.BaseMode}"); } catch (Exception exception) { Console.WriteLine($"Failed to decode HEARTBEAT: {exception.Message}"); log.Write($"HEARTBEAT decode failed: {exception}"); }
        return [];
    }
    private IEnumerable<OutgoingMessage> ProcessCommandLong(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) return [];
        CommandLongPacket command; try { command = new(); var readSpan = context.Frame.Span; command.Deserialize(ref readSpan); } catch (Exception exception) { Console.WriteLine($"Failed to decode COMMAND_LONG: {exception.Message}"); log.Write($"COMMAND_LONG decode failed: {exception}"); return []; }
        if (!TargetsThisSystem(command.Payload.TargetSystem)) return [];
        WarnIfTargetedAtUnregisteredComponent(context, command.Payload.TargetSystem, command.Payload.TargetComponent);
        var recipients = components.GetMessageRecipients(context.MessageId, command.Payload.TargetSystem, command.Payload.TargetComponent).ToList();
        return recipients.SelectMany(x => x.HandleMessage(context)).ToList();
    }
    private IEnumerable<OutgoingMessage> ProcessGimbalManagerSetAttitude(MavlinkMessageContext context)
    {
        GimbalManagerSetAttitudePacket command;
        try { command = new(); var readSpan = context.Frame.Span; command.Deserialize(ref readSpan); }
        catch (Exception exception) { log.Write($"GIMBAL_MANAGER_SET_ATTITUDE decode failed: {exception}"); return []; }

        if (!TargetsThisSystem(command.Payload.TargetSystem)) return [];
        WarnIfTargetedAtUnregisteredComponent(context, command.Payload.TargetSystem, command.Payload.TargetComponent);
        return components
            .GetMessageRecipients(context.MessageId, command.Payload.TargetSystem, command.Payload.TargetComponent)
            .SelectMany(x => x.HandleMessage(context))
            .ToList();
    }
    private IEnumerable<OutgoingMessage> ProcessGimbalDeviceSetAttitude(MavlinkMessageContext context)
    {
        GimbalDeviceSetAttitudePacket command;
        try { command = new(); var readSpan = context.Frame.Span; command.Deserialize(ref readSpan); }
        catch (Exception exception) { log.Write($"GIMBAL_DEVICE_SET_ATTITUDE decode failed: {exception}"); return []; }

        if (!TargetsThisSystem(command.Payload.TargetSystem)) return [];
        WarnIfTargetedAtUnregisteredComponent(context, command.Payload.TargetSystem, command.Payload.TargetComponent);
        return components
            .GetMessageRecipients(context.MessageId, command.Payload.TargetSystem, command.Payload.TargetComponent)
            .SelectMany(x => x.HandleMessage(context))
            .ToList();
    }

    private bool TargetsThisSystem(byte targetSystem) =>
        targetSystem == 0 || targetSystem == deviceSystemId;

    private void WarnIfTargetedAtUnregisteredComponent(
        MavlinkMessageContext context,
        byte targetSystem,
        byte targetComponent)
    {
        if (targetComponent == 0 || !TargetsThisSystem(targetSystem) ||
            components.Contains(deviceSystemId, targetComponent))
        {
            return;
        }

        var warning =
            $"WARNING: MAVLink message {context.MessageId} from {context.Source.SystemId}/{context.Source.ComponentId} " +
            $"targets {targetSystem}/{targetComponent}, but this server has no such component.";
        Console.WriteLine(warning);
        log.Write(warning);
    }
}
