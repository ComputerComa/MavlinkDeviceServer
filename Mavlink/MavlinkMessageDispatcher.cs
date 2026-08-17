using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkMessageDispatcher(byte deviceSystemId, ComponentRegistry components, DebugLog log)
{
    public IEnumerable<OutgoingMessage> Route(MavlinkMessageContext context) =>
        context.MessageId switch
        {
            0 => ProcessHeartbeat(context),
            76 => ProcessCommandLong(context),
            77 => ProcessCommandAck(context),
            284 => ProcessGimbalDeviceSetAttitude(context),
            286 => ProcessAutopilotStateForGimbalDevice(context),
            _ => []
        };

    public IEnumerable<ScheduledMessage> GetScheduledMessages() =>
        components.Components.SelectMany(component => component.GetScheduledMessages());
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
        if (command.Payload.TargetComponent != 0 &&
            !components.Contains(deviceSystemId, command.Payload.TargetComponent)) return [];

        if (command.Payload.Command == MavCmd.MavCmdRequestMessage)
        {
            var requestedMessageId = (uint)Math.Max(0, command.Payload.Param1);
            var recipient = components.GetRequestMessageRecipient(
                requestedMessageId,
                command.Payload.TargetSystem,
                command.Payload.TargetComponent);
            return recipient is null ? [] : recipient.HandleMessage(context).ToList();
        }

        // A broadcast COMMAND_LONG has no single component responsible for its ACK.
        if (command.Payload.TargetComponent == 0) return [];
        var target = components.GetMessageRecipients(
            context.MessageId,
            command.Payload.TargetSystem,
            command.Payload.TargetComponent).SingleOrDefault();
        return target is null ? [] : target.HandleMessage(context).ToList();
    }
    private IEnumerable<OutgoingMessage> ProcessCommandAck(MavlinkMessageContext context)
    {
        if (context.Frame.Span[0] != 0xFD) return [];

        try
        {
            var packet = new CommandAckPacket();
            var readSpan = context.Frame.Span;
            packet.Deserialize(ref readSpan);
            var target = packet.Payload.TargetSystem == 0 && packet.Payload.TargetComponent == 0
                ? string.Empty
                : $" Target={packet.Payload.TargetSystem}/{packet.Payload.TargetComponent}";
            var message =
                $"COMMAND_ACK from {packet.SystemId}/{packet.ComponentId} " +
                $"Command={packet.Payload.Command} Result={packet.Payload.Result}{target}";
            log.Write(message);
            if (ShouldWriteCommandAckToConsole(packet))
            {
                Console.WriteLine(message);
            }
        }
        catch (Exception exception)
        {
            log.Write($"COMMAND_ACK decode failed: {exception}");
        }

        return [];
    }

    private static bool ShouldWriteCommandAckToConsole(CommandAckPacket packet)
    {
        if (packet.Payload.Result != MavResult.MavResultAccepted)
        {
            return true;
        }

        return (int)packet.Payload.Command is
            205 or 220 or 511 or 1000 or 1001;
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
    private IEnumerable<OutgoingMessage> ProcessAutopilotStateForGimbalDevice(MavlinkMessageContext context)
    {
        AutopilotStateForGimbalDevicePacket packet;
        try { packet = new(); var readSpan = context.Frame.Span; packet.Deserialize(ref readSpan); }
        catch (Exception exception) { log.Write($"AUTOPILOT_STATE_FOR_GIMBAL_DEVICE decode failed: {exception}"); return []; }

        if (!TargetsThisSystem(packet.Payload.TargetSystem)) return [];
        WarnIfTargetedAtUnregisteredComponent(context, packet.Payload.TargetSystem, packet.Payload.TargetComponent);
        return components
            .GetMessageRecipients(context.MessageId, packet.Payload.TargetSystem, packet.Payload.TargetComponent)
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
