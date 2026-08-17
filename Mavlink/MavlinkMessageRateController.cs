using System.Collections.Concurrent;

namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkMessageRateController
{
    private readonly ConcurrentDictionary<MavlinkMessageScheduleKey, RateEntry> _entries = new();

    public void Register(ScheduledMessage message)
    {
        if (message.DefaultInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Default interval must be positive.");
        }

        if (!_entries.TryAdd(message.Key, new RateEntry(message.DefaultInterval)))
        {
            throw new InvalidOperationException($"Message schedule {message.Key} is already registered.");
        }
    }

    public bool TrySetInterval(
        MavlinkMessageScheduleKey key,
        TimeSpan interval,
        out TimeSpan effectiveInterval)
    {
        if (interval <= TimeSpan.Zero)
        {
            effectiveInterval = default;
            return false;
        }

        if (!_entries.TryGetValue(key, out var entry))
        {
            effectiveInterval = default;
            return false;
        }

        effectiveInterval = entry.SetInterval(interval);
        return true;
    }

    public bool TryRestoreDefault(MavlinkMessageScheduleKey key, out TimeSpan effectiveInterval) =>
        TryUpdate(key, entry => entry.RestoreDefault(), out effectiveInterval);

    public bool TryDisable(MavlinkMessageScheduleKey key) =>
        _entries.TryGetValue(key, out var entry) && entry.Disable();

    public MessageRate GetRate(MavlinkMessageScheduleKey key)
    {
        return _entries.TryGetValue(key, out var entry)
            ? entry.GetRate()
            : throw new InvalidOperationException($"Message schedule {key} has not been registered.");
    }

    private sealed class RateEntry(TimeSpan defaultInterval)
    {
        private readonly object _sync = new();
        private TimeSpan? _interval = defaultInterval;
        private long _version;

        public TimeSpan SetInterval(TimeSpan interval)
        {
            lock (_sync)
            {
                if (_interval == interval)
                {
                    return interval;
                }

                _interval = interval;
                _version++;
                return interval;
            }
        }

        public TimeSpan RestoreDefault() => SetInterval(defaultInterval);

        public bool Disable()
        {
            lock (_sync)
            {
                if (_interval is null) return true;
                _interval = null;
                _version++;
                return true;
            }
        }

        public MessageRate GetRate()
        {
            lock (_sync)
            {
                return new MessageRate(_interval, _version);
            }
        }
    }

    private bool TryUpdate(
        MavlinkMessageScheduleKey key,
        Func<RateEntry, TimeSpan> update,
        out TimeSpan effectiveInterval)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            effectiveInterval = default;
            return false;
        }

        effectiveInterval = update(entry);
        return true;
    }
}

public readonly record struct MessageRate(TimeSpan? Interval, long Version);
