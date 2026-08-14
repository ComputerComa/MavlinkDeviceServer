using Asv.Mavlink;

namespace MavlinkDeviceServer.Mavlink;

public static class MavlinkCodec
{
    public static byte[] Encode(MavlinkV2Message packet)
    {
        var buffer = new byte[MavlinkV2Protocol.PacketV2MaxSize];
        var writeSpan = buffer.AsSpan();
        packet.Serialize(ref writeSpan);
        return buffer[..(buffer.Length - writeSpan.Length)];
    }

    public static int FindNextFrame(ReadOnlySpan<byte> data)
    {
        for (var index = 0; index < data.Length; index++)
        {
            if (data[index] is 0xFD or 0xFE) return index;
        }

        return -1;
    }

    public static bool TryGetFrameLength(ReadOnlySpan<byte> data, out int frameLength)
    {
        frameLength = 0;
        if (data.Length < 2) return false;

        var payloadLength = data[1];
        if (data[0] == 0xFD)
        {
            if (data.Length < 10) return false;
            frameLength = 10 + payloadLength + 2 + ((data[2] & 0x01) != 0 ? 13 : 0);
        }
        else if (data[0] == 0xFE)
        {
            frameLength = 6 + payloadLength + 2;
        }
        else return false;

        return data.Length >= frameLength;
    }

    public static uint GetMessageId(ReadOnlySpan<byte> frame) => frame[0] switch
    {
        0xFD when frame.Length >= 10 => (uint)(frame[7] | (frame[8] << 8) | (frame[9] << 16)),
        0xFE when frame.Length >= 6 => frame[5],
        _ => uint.MaxValue
    };

    public static MavlinkIdentity GetSourceIdentity(ReadOnlySpan<byte> frame) => frame[0] switch
    {
        0xFD when frame.Length >= 10 => new(frame[5], frame[6]),
        0xFE when frame.Length >= 6 => new(frame[3], frame[4]),
        _ => new(0, 0)
    };
}

public readonly record struct MavlinkIdentity(byte SystemId, byte ComponentId);
