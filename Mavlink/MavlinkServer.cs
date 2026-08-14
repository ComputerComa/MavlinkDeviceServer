using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkServer(IPEndPoint listenEndpoint, MavlinkMessageDispatcher dispatcher, DebugLog log)
{
    private readonly ConcurrentDictionary<(byte SystemId, byte ComponentId), byte> _discoveredComponents = new();
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(listenEndpoint); IPEndPoint? remoteEndpoint = null; var telemetryTask = SendPeriodicTelemetryAsync(socket, () => remoteEndpoint, cancellationToken);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UdpReceiveResult datagram; try { datagram = await socket.ReceiveAsync(cancellationToken); } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                if (remoteEndpoint is null || !remoteEndpoint.Equals(datagram.RemoteEndPoint)) { remoteEndpoint = datagram.RemoteEndPoint; Console.WriteLine($"MAVLink endpoint detected: {remoteEndpoint}"); Console.WriteLine("Periodic telemetry transmission started."); Console.WriteLine(); await log.WriteAsync($"Remote endpoint set to {remoteEndpoint}"); }
                foreach (var response in ProcessDatagram(datagram.Buffer, datagram.RemoteEndPoint)) { var frame = MavlinkCodec.Encode(response.Packet); await socket.SendAsync(frame, datagram.RemoteEndPoint, cancellationToken); await log.WriteFrameAsync("TX", datagram.RemoteEndPoint, response.Description, frame); }
            }
        }
        catch (SocketException exception) when (cancellationToken.IsCancellationRequested) { await log.WriteAsync($"Socket closed during shutdown: {exception.Message}"); }
        finally { try { await telemetryTask; } catch (OperationCanceledException) { } }
    }
    private IEnumerable<OutgoingMessage> ProcessDatagram(byte[] buffer, IPEndPoint endpoint)
    {
        var responses = new List<OutgoingMessage>(); ReadOnlySpan<byte> remaining = buffer;
        while (!remaining.IsEmpty)
        {
            var offset = MavlinkCodec.FindNextFrame(remaining); if (offset < 0) { log.Write($"RX {endpoint} ignored {remaining.Length} non-MAVLink bytes"); break; }
            if (offset > 0) { log.Write($"RX {endpoint} skipped {offset} bytes before MAVLink frame"); remaining = remaining[offset..]; }
            if (!MavlinkCodec.TryGetFrameLength(remaining, out var length)) { log.Write($"RX {endpoint} incomplete frame; remaining={remaining.Length}"); break; }
            var frame = remaining[..length]; var messageId = MavlinkCodec.GetMessageId(frame); var source = MavlinkCodec.GetSourceIdentity(frame); log.WriteFrame("RX", endpoint, $"MSG={messageId} SYS={source.SystemId} COMP={source.ComponentId}", frame); DiscoverComponent(source, messageId); responses.AddRange(dispatcher.Route(new MavlinkMessageContext(messageId, frame.ToArray(), endpoint, log))); remaining = remaining[length..];
        }
        return responses;
    }
    private void DiscoverComponent(MavlinkIdentity source, uint messageId) { if (source.SystemId == 0 || !_discoveredComponents.TryAdd((source.SystemId, source.ComponentId), 0)) return; Console.WriteLine($"Discovered MAVLink component {source.SystemId}/{source.ComponentId} from message {messageId}"); log.Write($"Discovered component {source.SystemId}/{source.ComponentId}"); }
    private async Task SendPeriodicTelemetryAsync(UdpClient socket, Func<IPEndPoint?> getRemoteEndpoint, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1)); while (await timer.WaitForNextTickAsync(cancellationToken)) { var destination = getRemoteEndpoint(); if (destination is null) continue; foreach (var message in dispatcher.GetPeriodicMessages(DateTimeOffset.UtcNow)) { var frame = MavlinkCodec.Encode(message.Packet); await socket.SendAsync(frame, destination, cancellationToken); await log.WriteFrameAsync("TX", destination, message.Description, frame); } }
    }
}
