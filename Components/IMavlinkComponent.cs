using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;

public interface IMavlinkComponent { byte SystemId { get; } byte ComponentId { get; } IReadOnlyCollection<uint> HandledMessageIds { get; } IEnumerable<OutgoingMessage> HandleMessage(MavlinkMessageContext context); IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now); }
