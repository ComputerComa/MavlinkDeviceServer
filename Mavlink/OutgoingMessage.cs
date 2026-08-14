using Asv.Mavlink;
namespace MavlinkDeviceServer.Mavlink;

public sealed record OutgoingMessage(MavlinkV2Message Packet, string Description);
