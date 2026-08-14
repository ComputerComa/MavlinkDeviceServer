namespace MavlinkDeviceServer.Gimbal;

public sealed class FakeGimbalDevice : IGimbalDevice
{
    private readonly object _sync = new();
    private GimbalState _state = new(0, 0, 0, 0, 0, 0, GimbalYawFrame.Vehicle);

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
                float.IsFinite(yawRate) ? yawRate : _state.YawRateRadiansPerSecond,
                _state.YawFrame);
        }

        return true;
    }

    public void SetRates(float rollRate, float pitchRate, float yawRate)
    {
        lock (_sync)
        {
            _state = _state with
            {
                RollRateRadiansPerSecond = float.IsFinite(rollRate) ? rollRate : _state.RollRateRadiansPerSecond,
                PitchRateRadiansPerSecond = float.IsFinite(pitchRate) ? pitchRate : _state.PitchRateRadiansPerSecond,
                YawRateRadiansPerSecond = float.IsFinite(yawRate) ? yawRate : _state.YawRateRadiansPerSecond
            };
        }
    }

    public void SetYawFrame(GimbalYawFrame yawFrame)
    {
        lock (_sync)
        {
            _state = _state with { YawFrame = yawFrame };
        }
    }

    public void Center()
    {
        lock (_sync)
        {
            _state = new GimbalState(0, 0, 0, 0, 0, 0, GimbalYawFrame.Vehicle);
        }
    }
}
