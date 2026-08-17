namespace MavlinkDeviceServer.Mavlink;

public readonly record struct MavlinkMessageScheduleKey(
    byte SystemId,
    byte ComponentId,
    uint MessageId);
