namespace MavlinkDeviceServer.Gimbal;

public sealed class FakeGimbalDevice : IGimbalDevice
{
    private readonly object _sync = new();
    private GimbalState _state = new(0, 0, 0, 0, 0, 0);

    public GimbalLimits Limits => GimbalLimits.Fake;

    public GimbalState State
    {
        get { lock (_sync) return _state; }
    }

    public bool SetAttitude(GimbalQuaternion attitude, float rollRate, float pitchRate, float yawRate)
    {
        if (!attitude.TryToEuler(out var roll, out var pitch, out var yaw)) return false;

        lock (_sync)
        {
            _state = new GimbalState(
                Math.Clamp(roll, Limits.RollMinRadians, Limits.RollMaxRadians),
                Math.Clamp(pitch, Limits.PitchMinRadians, Limits.PitchMaxRadians),
                Math.Clamp(yaw, Limits.YawMinRadians, Limits.YawMaxRadians),
                float.IsFinite(rollRate) ? rollRate : _state.RollRateRadiansPerSecond,
                float.IsFinite(pitchRate) ? pitchRate : _state.PitchRateRadiansPerSecond,
                float.IsFinite(yawRate) ? yawRate : _state.YawRateRadiansPerSecond);
        }

        return true;
    }

    public void Center()
    {
        lock (_sync)
        {
            _state = new GimbalState(0, 0, 0, 0, 0, 0);
        }
    }
}
