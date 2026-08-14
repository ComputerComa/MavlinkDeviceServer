namespace MavlinkDeviceServer.Gimbal;

public interface IGimbalDevice
{
    GimbalLimits Limits { get; }
    GimbalState State { get; }
    bool SetAttitude(GimbalQuaternion attitude, float rollRate, float pitchRate, float yawRate);
    void Center();
}
