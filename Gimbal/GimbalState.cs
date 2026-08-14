namespace MavlinkDeviceServer.Gimbal;

public sealed record GimbalLimits(
    float RollMinRadians,
    float RollMaxRadians,
    float PitchMinRadians,
    float PitchMaxRadians,
    float YawMinRadians,
    float YawMaxRadians)
{
    public static GimbalLimits Fake { get; } = new(
        DegreesToRadians(-45), DegreesToRadians(45),
        DegreesToRadians(-90), DegreesToRadians(30),
        DegreesToRadians(-180), DegreesToRadians(180));

    private static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
}

public sealed record GimbalState(
    float RollRadians,
    float PitchRadians,
    float YawRadians,
    float RollRateRadiansPerSecond,
    float PitchRateRadiansPerSecond,
    float YawRateRadiansPerSecond);

public readonly record struct GimbalQuaternion(float W, float X, float Y, float Z)
{
    public static GimbalQuaternion FromEuler(float roll, float pitch, float yaw)
    {
        var halfRoll = roll / 2f;
        var halfPitch = pitch / 2f;
        var halfYaw = yaw / 2f;
        var cr = MathF.Cos(halfRoll);
        var sr = MathF.Sin(halfRoll);
        var cp = MathF.Cos(halfPitch);
        var sp = MathF.Sin(halfPitch);
        var cy = MathF.Cos(halfYaw);
        var sy = MathF.Sin(halfYaw);

        return new GimbalQuaternion(
            cr * cp * cy + sr * sp * sy,
            sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy,
            cr * cp * sy - sr * sp * cy);
    }

    public bool TryToEuler(out float roll, out float pitch, out float yaw)
    {
        roll = pitch = yaw = 0;
        var length = MathF.Sqrt(W * W + X * X + Y * Y + Z * Z);
        if (!float.IsFinite(length) || length < 0.00001f) return false;

        var w = W / length;
        var x = X / length;
        var y = Y / length;
        var z = Z / length;
        roll = MathF.Atan2(2f * (w * x + y * z), 1f - 2f * (x * x + y * y));
        pitch = MathF.Asin(Math.Clamp(2f * (w * y - z * x), -1f, 1f));
        yaw = MathF.Atan2(2f * (w * z + x * y), 1f - 2f * (y * y + z * z));
        return float.IsFinite(roll) && float.IsFinite(pitch) && float.IsFinite(yaw);
    }
}
