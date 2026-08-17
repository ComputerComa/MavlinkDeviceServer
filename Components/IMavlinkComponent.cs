using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;

public interface IMavlinkComponent
{
    byte SystemId { get; }
    byte ComponentId { get; }
    IReadOnlyCollection<uint> HandledMessageIds { get; }
    IReadOnlyCollection<uint> HandledRequestMessageIds { get; }
    IEnumerable<OutgoingMessage> HandleMessage(MavlinkMessageContext context);
    IEnumerable<ScheduledMessage> GetScheduledMessages();
}
