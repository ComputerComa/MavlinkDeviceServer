using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Asv.Mavlink;
using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;

internal static class Program
{
    private const byte DeviceSystemId = 1;

    private const byte DeviceComponentId =
        (byte)MavComponent.MavCompIdOnboardComputer;

    private const int ListenPort = 14560;

    private const uint HeartbeatMessageId = 0;
    private const uint CommandLongMessageId = 76;
    private const uint OnboardComputerStatusMessageId = 390;

    private static readonly ConcurrentDictionary<
        (byte SystemId, byte ComponentId),
        byte> DiscoveredComponents = new();

    private static int _packetSequence = -1;

    private static async Task Main()
    {
        var listenEndpoint = new IPEndPoint(
            IPAddress.Any,
            ListenPort);

        Directory.CreateDirectory("logs");

        var logPath = Path.Combine(
            "logs",
            $"mavlink-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        await using var debugLog = new DebugLog(logPath);
        using var socket = new UdpClient(listenEndpoint);
        using var shutdown = new CancellationTokenSource();

        IPEndPoint? remoteEndpoint = null;

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;

            Console.WriteLine();
            Console.WriteLine("Shutdown requested...");

            shutdown.Cancel();
        };

        Console.WriteLine("MAVLink Device Server — SITL test");
        Console.WriteLine($"Listening: {listenEndpoint}");
        Console.WriteLine($"Debug log: {Path.GetFullPath(logPath)}");
        Console.WriteLine("Waiting for MAVLink traffic...");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        await debugLog.WriteAsync(
            $"Server started; listening on {listenEndpoint}");

        var telemetryTask = SendPeriodicTelemetryAsync(
            socket,
            () => remoteEndpoint,
            debugLog,
            shutdown.Token);

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                UdpReceiveResult datagram;

                try
                {
                    datagram = await socket.ReceiveAsync(
                        shutdown.Token);
                }
                catch (OperationCanceledException)
                    when (shutdown.IsCancellationRequested)
                {
                    break;
                }

                if (remoteEndpoint is null ||
                    !remoteEndpoint.Equals(datagram.RemoteEndPoint))
                {
                    remoteEndpoint = datagram.RemoteEndPoint;

                    Console.WriteLine(
                        $"MAVLink endpoint detected: {remoteEndpoint}");

                    Console.WriteLine(
                        "Periodic telemetry transmission started.");

                    Console.WriteLine();

                    await debugLog.WriteAsync(
                        $"Remote endpoint set to {remoteEndpoint}");
                }

                var responses = ProcessDatagram(
                    datagram.Buffer,
                    datagram.RemoteEndPoint,
                    debugLog);

                foreach (var response in responses)
                {
                    await socket.SendAsync(
                        response.Frame,
                        datagram.RemoteEndPoint,
                        shutdown.Token);

                    await debugLog.WriteFrameAsync(
                        "TX",
                        datagram.RemoteEndPoint,
                        response.Description,
                        response.Frame);
                }
            }
        }
        catch (SocketException exception)
            when (shutdown.IsCancellationRequested)
        {
            await debugLog.WriteAsync(
                $"Socket closed during shutdown: {exception.Message}");
        }
        finally
        {
            shutdown.Cancel();

            try
            {
                await telemetryTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during normal shutdown.
            }

            await debugLog.WriteAsync("Server stopped cleanly");
        }

        Console.WriteLine("MAVLink listener stopped cleanly.");
    }

    private static List<OutgoingFrame> ProcessDatagram(
        byte[] buffer,
        IPEndPoint remoteEndpoint,
        DebugLog debugLog)
    {
        var responses = new List<OutgoingFrame>();
        ReadOnlySpan<byte> remaining = buffer;

        while (!remaining.IsEmpty)
        {
            var frameOffset = FindNextFrame(remaining);

            if (frameOffset < 0)
            {
                debugLog.Write(
                    $"RX {remoteEndpoint} ignored " +
                    $"{remaining.Length} non-MAVLink bytes");

                break;
            }

            if (frameOffset > 0)
            {
                debugLog.Write(
                    $"RX {remoteEndpoint} skipped " +
                    $"{frameOffset} bytes before MAVLink frame");

                remaining = remaining[frameOffset..];
            }

            if (!TryGetFrameLength(
                    remaining,
                    out var frameLength))
            {
                debugLog.Write(
                    $"RX {remoteEndpoint} incomplete frame; " +
                    $"remaining={remaining.Length}");

                break;
            }

            var frame = remaining[..frameLength];
            var messageId = GetMessageId(frame);
            var identity = GetSourceIdentity(frame);

            debugLog.WriteFrame(
                "RX",
                remoteEndpoint,
                $"MSG={messageId} " +
                $"SYS={identity.SystemId} " +
                $"COMP={identity.ComponentId}",
                frame);

            DiscoverComponent(
                identity.SystemId,
                identity.ComponentId,
                messageId,
                debugLog);

            switch (messageId)
            {
                case HeartbeatMessageId:
                    ProcessHeartbeat(frame, debugLog);
                    break;

                case CommandLongMessageId:
                    ProcessCommandLong(
                        frame,
                        responses,
                        debugLog);
                    break;
            }

            remaining = remaining[frameLength..];
        }

        return responses;
    }

    private static void DiscoverComponent(
        byte systemId,
        byte componentId,
        uint messageId,
        DebugLog debugLog)
    {
        if (systemId == 0)
        {
            return;
        }

        if (!DiscoveredComponents.TryAdd(
                (systemId, componentId),
                0))
        {
            return;
        }

        Console.WriteLine(
            $"Discovered MAVLink component " +
            $"{systemId}/{componentId} " +
            $"from message {messageId}");

        debugLog.Write(
            $"Discovered component {systemId}/{componentId}");
    }

    private static void ProcessHeartbeat(
        ReadOnlySpan<byte> frame,
        DebugLog debugLog)
    {
        if (frame[0] != 0xFD)
        {
            debugLog.Write(
                "MAVLink 1 HEARTBEAT received; " +
                "this prototype decodes MAVLink 2 only");

            return;
        }

        try
        {
            var heartbeat = new HeartbeatPacket();
            var readSpan = frame;

            heartbeat.Deserialize(ref readSpan);

            debugLog.Write(
                $"HEARTBEAT decoded " +
                $"SYS={heartbeat.SystemId} " +
                $"COMP={heartbeat.ComponentId} " +
                $"TYPE={heartbeat.Payload.Type} " +
                $"AUTOPILOT={heartbeat.Payload.Autopilot} " +
                $"STATE={heartbeat.Payload.SystemStatus} " +
                $"BASE_MODE={heartbeat.Payload.BaseMode}");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Failed to decode HEARTBEAT: " +
                $"{exception.Message}");

            debugLog.Write(
                $"HEARTBEAT decode failed: {exception}");
        }
    }

    private static void ProcessCommandLong(
        ReadOnlySpan<byte> frame,
        List<OutgoingFrame> responses,
        DebugLog debugLog)
    {
        if (frame[0] != 0xFD)
        {
            debugLog.Write(
                "MAVLink 1 COMMAND_LONG received but ignored");

            return;
        }

        CommandLongPacket command;

        try
        {
            command = new CommandLongPacket();

            var readSpan = frame;
            command.Deserialize(ref readSpan);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"Failed to decode COMMAND_LONG: " +
                $"{exception.Message}");

            debugLog.Write(
                $"COMMAND_LONG decode failed: {exception}");

            return;
        }

        Console.WriteLine(
            $"COMMAND_LONG received from " +
            $"{command.SystemId}/{command.ComponentId}");

        Console.WriteLine(
            $"  Command: {command.Payload.Command}");

        Console.WriteLine(
            $"  Target:  " +
            $"{command.Payload.TargetSystem}/" +
            $"{command.Payload.TargetComponent}");

        Console.WriteLine(
            $"  Param 1: {command.Payload.Param1}");

        debugLog.Write(
            $"COMMAND_LONG decoded " +
            $"SOURCE={command.SystemId}/{command.ComponentId} " +
            $"TARGET={command.Payload.TargetSystem}/" +
            $"{command.Payload.TargetComponent} " +
            $"COMMAND={command.Payload.Command} " +
            $"CONFIRMATION={command.Payload.Confirmation} " +
            $"PARAMS=[" +
            $"{command.Payload.Param1}, " +
            $"{command.Payload.Param2}, " +
            $"{command.Payload.Param3}, " +
            $"{command.Payload.Param4}, " +
            $"{command.Payload.Param5}, " +
            $"{command.Payload.Param6}, " +
            $"{command.Payload.Param7}]");

        if (!IsCommandForThisDevice(command))
        {
            Console.WriteLine(
                "  Ignored: command targets another component.");

            Console.WriteLine();

            debugLog.Write(
                "COMMAND_LONG ignored because target does not match");

            return;
        }

        if (command.Payload.Command ==
            MavCmd.MavCmdRequestMessage)
        {
            HandleRequestMessage(
                command,
                responses,
                debugLog);

            return;
        }

        Console.WriteLine(
            "  Result: Unsupported command");

        Console.WriteLine();

        responses.Add(new OutgoingFrame(
            Encode(CreateCommandAck(
                command,
                MavResult.MavResultUnsupported)),
            $"COMMAND_ACK " +
            $"{command.Payload.Command} " +
            $"{MavResult.MavResultUnsupported}"));

        debugLog.Write(
            $"COMMAND_LONG unsupported: " +
            $"{command.Payload.Command}");
    }

    private static void HandleRequestMessage(
        CommandLongPacket command,
        List<OutgoingFrame> responses,
        DebugLog debugLog)
    {
        var requestedMessageId =
            (uint)Math.Max(0, command.Payload.Param1);

        Console.WriteLine(
            $"  Requested message ID: {requestedMessageId}");

        if (requestedMessageId ==
            OnboardComputerStatusMessageId)
        {
            responses.Add(new OutgoingFrame(
                Encode(CreateOnboardComputerStatus()),
                "ONBOARD_COMPUTER_STATUS requested response"));

            responses.Add(new OutgoingFrame(
                Encode(CreateCommandAck(
                    command,
                    MavResult.MavResultAccepted)),
                $"COMMAND_ACK " +
                $"{command.Payload.Command} " +
                $"{MavResult.MavResultAccepted}"));

            Console.WriteLine(
                "  Result: ONBOARD_COMPUTER_STATUS sent");

            Console.WriteLine(
                "  ACK:    Accepted");

            Console.WriteLine();

            debugLog.Write(
                "MAV_CMD_REQUEST_MESSAGE accepted for " +
                "ONBOARD_COMPUTER_STATUS");

            return;
        }

        responses.Add(new OutgoingFrame(
            Encode(CreateCommandAck(
                command,
                MavResult.MavResultUnsupported)),
            $"COMMAND_ACK " +
            $"{command.Payload.Command} " +
            $"{MavResult.MavResultUnsupported}"));

        Console.WriteLine(
            $"  Result: Message {requestedMessageId} unsupported");

        Console.WriteLine(
            "  ACK:    Unsupported");

        Console.WriteLine();

        debugLog.Write(
            $"Requested message {requestedMessageId} unsupported");
    }

    private static bool IsCommandForThisDevice(
        CommandLongPacket command)
    {
        var targetSystem =
            command.Payload.TargetSystem;

        var targetComponent =
            command.Payload.TargetComponent;

        var systemMatches =
            targetSystem == 0 ||
            targetSystem == DeviceSystemId;

        var componentMatches =
            targetComponent == 0 ||
            targetComponent == DeviceComponentId;

        return systemMatches && componentMatches;
    }

    private static CommandAckPacket CreateCommandAck(
        CommandLongPacket command,
        MavResult result)
    {
        var ack = new CommandAckPacket
        {
            SystemId = DeviceSystemId,
            ComponentId = DeviceComponentId,
            Sequence = NextSequence()
        };

        ack.Payload.Command =
            command.Payload.Command;

        ack.Payload.Result = result;
        ack.Payload.Progress = 0;
        ack.Payload.ResultParam2 = 0;

        ack.Payload.TargetSystem =
            command.SystemId;

        ack.Payload.TargetComponent =
            command.ComponentId;

        return ack;
    }

    private static async Task SendPeriodicTelemetryAsync(
        UdpClient socket,
        Func<IPEndPoint?> getRemoteEndpoint,
        DebugLog debugLog,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1));

        var statusCounter = 0;

        while (await timer.WaitForNextTickAsync(
                   cancellationToken))
        {
            var destination = getRemoteEndpoint();

            if (destination is null)
            {
                continue;
            }

            var heartbeatFrame =
                Encode(CreateDeviceHeartbeat());

            await socket.SendAsync(
                heartbeatFrame,
                destination,
                cancellationToken);

            await debugLog.WriteFrameAsync(
                "TX",
                destination,
                $"HEARTBEAT " +
                $"SYS={DeviceSystemId} " +
                $"COMP={DeviceComponentId}",
                heartbeatFrame);

            statusCounter++;

            // ONBOARD_COMPUTER_STATUS at 1 Hz for this test.
            if (statusCounter >= 5)
            {
                statusCounter = 0;

                var statusFrame =
                    Encode(CreateOnboardComputerStatus());

                await socket.SendAsync(
                    statusFrame,
                    destination,
                    cancellationToken);

                await debugLog.WriteFrameAsync(
                    "TX",
                    destination,
                    "ONBOARD_COMPUTER_STATUS",
                    statusFrame);
            }
        }
    }

    private static HeartbeatPacket CreateDeviceHeartbeat()
    {
        var heartbeat = new HeartbeatPacket
        {
            SystemId = DeviceSystemId,
            ComponentId = DeviceComponentId,
            Sequence = NextSequence()
        };

        heartbeat.Payload.Type =
            MavType.MavTypeOnboardController;

        heartbeat.Payload.Autopilot =
            MavAutopilot.MavAutopilotInvalid;

        heartbeat.Payload.BaseMode = 0;
        heartbeat.Payload.CustomMode = 0;

        heartbeat.Payload.SystemStatus =
            MavState.MavStateActive;

        heartbeat.Payload.MavlinkVersion = 3;

        return heartbeat;
    }

    private static OnboardComputerStatusPacket
        CreateOnboardComputerStatus()
    {
        var status = new OnboardComputerStatusPacket
        {
            SystemId = DeviceSystemId,
            ComponentId = DeviceComponentId,
            Sequence = NextSequence()
        };

        status.Payload.TimeUsec =
            (ulong)DateTimeOffset.UtcNow
                .ToUnixTimeMilliseconds() * 1_000UL;

        status.Payload.Uptime =
            unchecked((uint)Math.Min(
                Environment.TickCount64,
                uint.MaxValue));

        // Leave host metrics unknown for now. Later, a Linux telemetry
        // service will populate these from /proc and /sys.
        status.Payload.RamTotal = 0;
        status.Payload.RamUsage = 0;

        return status;
    }

    private static byte NextSequence()
    {
        return unchecked(
            (byte)Interlocked.Increment(
                ref _packetSequence));
    }

    private static byte[] Encode(
        MavlinkV2Message packet)
    {
        var buffer =
            new byte[MavlinkV2Protocol.PacketV2MaxSize];

        var writeSpan = buffer.AsSpan();

        packet.Serialize(ref writeSpan);

        var encodedLength =
            buffer.Length - writeSpan.Length;

        return buffer[..encodedLength];
    }

    private static int FindNextFrame(
        ReadOnlySpan<byte> data)
    {
        for (var index = 0;
             index < data.Length;
             index++)
        {
            if (data[index] is 0xFD or 0xFE)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetFrameLength(
        ReadOnlySpan<byte> data,
        out int frameLength)
    {
        frameLength = 0;

        if (data.Length < 2)
        {
            return false;
        }

        var magic = data[0];
        var payloadLength = data[1];

        if (magic == 0xFD)
        {
            const int headerLength = 10;
            const int checksumLength = 2;
            const int signatureLength = 13;

            if (data.Length < headerLength)
            {
                return false;
            }

            var isSigned =
                (data[2] & 0x01) != 0;

            frameLength =
                headerLength +
                payloadLength +
                checksumLength +
                (isSigned ? signatureLength : 0);
        }
        else if (magic == 0xFE)
        {
            const int headerLength = 6;
            const int checksumLength = 2;

            frameLength =
                headerLength +
                payloadLength +
                checksumLength;
        }
        else
        {
            return false;
        }

        return data.Length >= frameLength;
    }

    private static uint GetMessageId(
        ReadOnlySpan<byte> frame)
    {
        return frame[0] switch
        {
            0xFD when frame.Length >= 10 =>
                (uint)(
                    frame[7] |
                    (frame[8] << 8) |
                    (frame[9] << 16)),

            0xFE when frame.Length >= 6 =>
                frame[5],

            _ => uint.MaxValue
        };
    }

    private static MavlinkIdentity GetSourceIdentity(
        ReadOnlySpan<byte> frame)
    {
        return frame[0] switch
        {
            0xFD when frame.Length >= 10 =>
                new MavlinkIdentity(
                    frame[5],
                    frame[6]),

            0xFE when frame.Length >= 6 =>
                new MavlinkIdentity(
                    frame[3],
                    frame[4]),

            _ => new MavlinkIdentity(0, 0)
        };
    }

    private sealed record OutgoingFrame(
        byte[] Frame,
        string Description);

    private sealed class DebugLog : IAsyncDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _sync = new();

        public DebugLog(string path)
        {
            _writer = new StreamWriter(
                path,
                append: false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = false
            };
        }

        public void Write(string message)
        {
            lock (_sync)
            {
                _writer.WriteLine(
                    $"{DateTimeOffset.Now:O} {message}");
            }
        }

        public void WriteFrame(
            string direction,
            IPEndPoint endpoint,
            string description,
            ReadOnlySpan<byte> frame)
        {
            var hexadecimal =
                Convert.ToHexString(frame);

            Write(
                $"{direction} {endpoint} " +
                $"{description} " +
                $"LEN={frame.Length} " +
                $"FRAME={hexadecimal}");
        }

        public Task WriteAsync(string message)
        {
            Write(message);
            return Task.CompletedTask;
        }

        public Task WriteFrameAsync(
            string direction,
            IPEndPoint endpoint,
            string description,
            byte[] frame)
        {
            WriteFrame(
                direction,
                endpoint,
                description,
                frame);

            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            lock (_sync)
            {
                _writer.Flush();
            }

            await _writer.DisposeAsync();
        }
    }
}