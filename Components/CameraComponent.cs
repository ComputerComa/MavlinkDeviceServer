using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;
// This intentionally has no C12 transport dependency. It will later expose
// CAMERA_INFORMATION, CAMERA_SETTINGS, and CAMERA_CAPTURE_STATUS from an adapter.
public sealed class CameraComponent(byte systemId, byte componentId) : MavlinkComponentBase(systemId, componentId)
{
    public override IReadOnlyCollection<uint> HandledMessageIds { get; } = [];

    public override IEnumerable<OutgoingMessage> GetPeriodicMessages(DateTimeOffset now) =>
        [new(CreateHeartbeat(), $"HEARTBEAT SYS={SystemId} COMP={ComponentId}")];

    private HeartbeatPacket CreateHeartbeat()
    {
        var packet = new HeartbeatPacket
        {
            SystemId = SystemId,
            ComponentId = ComponentId,
            Sequence = NextSequence()
        };

        packet.Payload.Type = MavType.MavTypeCamera;
        packet.Payload.Autopilot = MavAutopilot.MavAutopilotInvalid;
        packet.Payload.SystemStatus = MavState.MavStateActive;
        packet.Payload.MavlinkVersion = 3;
        return packet;
    }
}
