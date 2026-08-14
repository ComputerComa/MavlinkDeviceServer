using MavlinkDeviceServer.Mavlink;
namespace MavlinkDeviceServer.Components;
// This intentionally has no C12 transport dependency. It will later expose
// CAMERA_INFORMATION, CAMERA_SETTINGS, and CAMERA_CAPTURE_STATUS from an adapter.
public sealed class CameraComponent(byte systemId, byte componentId) : MavlinkComponentBase(systemId, componentId) { public override IReadOnlyCollection<uint> HandledMessageIds { get; } = []; }
