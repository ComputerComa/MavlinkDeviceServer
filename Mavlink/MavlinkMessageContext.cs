using System.Net;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkMessageContext
{
    public MavlinkMessageContext(uint messageId, ReadOnlyMemory<byte> frame, IPEndPoint remoteEndpoint, DebugLog log) { MessageId = messageId; Frame = frame; RemoteEndpoint = remoteEndpoint; Log = log; Source = MavlinkCodec.GetSourceIdentity(frame.Span); }
    public uint MessageId { get; }
    public ReadOnlyMemory<byte> Frame { get; }
    public IPEndPoint RemoteEndpoint { get; }
    public MavlinkIdentity Source { get; }
    public DebugLog Log { get; }
}
