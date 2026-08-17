namespace MavlinkDeviceServer.Mavlink;

public sealed record ScheduledMessage(
    MavlinkMessageScheduleKey Key,
    TimeSpan DefaultInterval,
    Func<OutgoingMessage> CreateMessage,
    string? FirstTransmissionDescription = null);
