namespace MavlinkDeviceServer.Gimbal;

public interface IGimbalDevice
{
    GimbalLimits Limits { get; }
    GimbalState State { get; }
    bool SetAttitude(GimbalQuaternion attitude, float rollRate, float pitchRate, float yawRate);
    void SetRates(float rollRate, float pitchRate, float yawRate);
    void SetYawFrame(GimbalYawFrame yawFrame);
    void SetAutopilotState(GimbalAutopilotState autopilotState);
    void Center();
}
