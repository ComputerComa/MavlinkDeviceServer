namespace MavlinkDeviceServer.Gimbal;

public sealed class FakeGimbalDevice : IGimbalDevice
{
    private readonly object _sync = new();
    private GimbalState _state = new(0, 0, 0, 0, 0, 0, GimbalYawFrame.Vehicle, GimbalPosition.Active);
    private GimbalAutopilotState? _autopilotState;

    public GimbalLimits Limits => GimbalLimits.Fake;

    public GimbalState State
    {
        get { lock (_sync) return _state; }
    }

    public GimbalAutopilotState? AutopilotState
    {
        get { lock (_sync) return _autopilotState; }
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
                _state.YawFrame,
                GimbalPosition.Active);
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
                YawRateRadiansPerSecond = float.IsFinite(yawRate) ? yawRate : _state.YawRateRadiansPerSecond,
                Position = GimbalPosition.Active
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

    public void SetAutopilotState(GimbalAutopilotState autopilotState)
    {
        lock (_sync)
        {
            _autopilotState = autopilotState;
        }
    }

    public void Center()
    {
        lock (_sync)
        {
            _state = new GimbalState(0, 0, 0, 0, 0, 0, GimbalYawFrame.Vehicle, GimbalPosition.Active);
        }
    }

    public void Neutral() => SetPosition(GimbalPosition.Neutral);

    public void Retract() => SetPosition(GimbalPosition.Retracted);

    private void SetPosition(GimbalPosition position)
    {
        lock (_sync)
        {
            // The fake device has no separate physical stow geometry yet.
            // Both special positions are represented by a centered attitude.
            _state = new GimbalState(0, 0, 0, 0, 0, 0, GimbalYawFrame.Vehicle, position);
        }
    }
}
