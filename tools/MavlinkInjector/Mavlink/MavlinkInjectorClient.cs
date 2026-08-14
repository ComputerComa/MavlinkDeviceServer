using System.Net;
using System.Net.Sockets;
using Asv.Mavlink;

namespace MavlinkInjector.Mavlink;

public sealed class MavlinkInjectorClient
{
    private readonly IPEndPoint _destination;

    public MavlinkInjectorClient(string host, ushort port)
    {
        if (port == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            throw new ArgumentException("Host must be an IP address.", nameof(host));
        }

        _destination = new IPEndPoint(address, port);
    }

    public IPEndPoint Destination => _destination;

    public byte[] Serialize(MavlinkV2Message packet)
    {
        var buffer = new byte[MavlinkV2Protocol.PacketV2MaxSize];
        var writeSpan = buffer.AsSpan();
        packet.Serialize(ref writeSpan);
        return buffer[..(buffer.Length - writeSpan.Length)];
    }

    public async Task SendAsync(byte[] frame)
    {
        using var udp = new UdpClient();
        await udp.SendAsync(frame, _destination);
    }
}
