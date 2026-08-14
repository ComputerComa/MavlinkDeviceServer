using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using MavlinkDeviceServer.Logging;
namespace MavlinkDeviceServer.Mavlink;

public sealed class MavlinkServer(IPEndPoint listenEndpoint, MavlinkMessageDispatcher dispatcher, DebugLog log)
{
    private readonly ConcurrentDictionary<(byte SystemId, byte ComponentId), byte> _discoveredComponents = new();
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var socket = new UdpClient(listenEndpoint);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IPEndPoint? remoteEndpoint = null;
        var telemetryTask = SendPeriodicTelemetryAsync(socket, () => remoteEndpoint, runCancellation.Token);
        _ = telemetryTask.ContinueWith(
            _ => runCancellation.Cancel(),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        Exception? receiveFailure = null;
        Exception? telemetryFailure = null;

        try
        {
            while (!runCancellation.IsCancellationRequested)
            {
                UdpReceiveResult datagram;
                try { datagram = await socket.ReceiveAsync(runCancellation.Token); }
                catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { break; }
                catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionReset)
                {
                    await log.WriteAsync($"UDP receive reset ({exception.ErrorCode}); continuing to listen: {exception.Message}");
                    continue;
                }
                if (remoteEndpoint is null || !remoteEndpoint.Equals(datagram.RemoteEndPoint))
                {
                    // TODO: Use an explicitly configured mavlink-router destination before production.
                    // A temporary sender can currently replace this learned telemetry destination.
                    remoteEndpoint = datagram.RemoteEndPoint;
                    Console.WriteLine($"MAVLink endpoint detected: {remoteEndpoint}");
                    Console.WriteLine("Periodic telemetry transmission started.");
                    Console.WriteLine();
                    await log.WriteAsync($"Remote endpoint set to {remoteEndpoint}");
                }
                foreach (var response in ProcessDatagram(datagram.Buffer, datagram.RemoteEndPoint)) { var frame = MavlinkCodec.Encode(response.Packet); await socket.SendAsync(frame, datagram.RemoteEndPoint, runCancellation.Token); await log.WriteFrameAsync("TX", datagram.RemoteEndPoint, response.Description, frame); }
            }
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
        catch (SocketException exception) when (runCancellation.IsCancellationRequested) { await log.WriteAsync($"Socket closed during shutdown: {exception.Message}"); }
        catch (Exception exception)
        {
            receiveFailure = exception;
            await log.WriteAsync($"MAVLink receive loop failed: {exception}");
        }
        finally
        {
            runCancellation.Cancel();
            try { await telemetryTask; }
            catch (OperationCanceledException) when (runCancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                telemetryFailure = exception;
                await log.WriteAsync($"MAVLink telemetry loop failed: {exception}");
            }
        }

        if (receiveFailure is not null) ExceptionDispatchInfo.Capture(receiveFailure).Throw();
        if (telemetryFailure is not null) ExceptionDispatchInfo.Capture(telemetryFailure).Throw();
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
