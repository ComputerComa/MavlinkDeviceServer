using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;

public abstract class MavlinkComponentBase(byte systemId, byte componentId) : IMavlinkComponent
{
    private int _packetSequence = -1;
    public byte SystemId { get; } = systemId; public byte ComponentId { get; } = componentId;
    public abstract IReadOnlyCollection<uint> HandledMessageIds { get; }
    public virtual IReadOnlyCollection<uint> HandledRequestMessageIds { get; } = [];
    public virtual IEnumerable<OutgoingMessage> HandleMessage(MavlinkMessageContext context) => [];
    public virtual IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now) => [];
    protected byte NextSequence() => unchecked((byte)Interlocked.Increment(ref _packetSequence));
}
