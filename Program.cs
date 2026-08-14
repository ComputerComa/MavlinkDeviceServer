using System.Net;
using Asv.Mavlink;
using Asv.Mavlink.Common;
using Asv.Mavlink.Minimal;
using MavlinkDeviceServer.Components;
using MavlinkDeviceServer.Logging;
using MavlinkDeviceServer.Mavlink;

internal static class Program
{
    private const byte DeviceSystemId = 1;
    private const int ListenPort = 14551;

    private static async Task Main()
    {
        var listenEndpoint = new IPEndPoint(IPAddress.Any, ListenPort);
        Directory.CreateDirectory("logs");
        var logPath = Path.Combine("logs", $"mavlink-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        await using var debugLog = new DebugLog(logPath);
        using var shutdown = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Console.WriteLine();
            Console.WriteLine("Shutdown requested...");
            shutdown.Cancel();
        };

        var components = new ComponentRegistry();
        components.Register(new OnboardComputerComponent(DeviceSystemId, (byte)MavComponent.MavCompIdOnboardComputer));
        components.Register(new GimbalComponent(DeviceSystemId, 154));
        components.Register(new CameraComponent(DeviceSystemId, 100));

        var router = new MavlinkRouter(DeviceSystemId, components, debugLog);
        var server = new MavlinkServer(listenEndpoint, router, debugLog);

        Console.WriteLine("MAVLink Device Server — SITL test");
        Console.WriteLine($"Listening: {listenEndpoint}");
        Console.WriteLine($"Debug log: {Path.GetFullPath(logPath)}");
        Console.WriteLine("Waiting for MAVLink traffic...");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();
        await debugLog.WriteAsync($"Server started; listening on {listenEndpoint}");

        try { await server.RunAsync(shutdown.Token); }
        finally { await debugLog.WriteAsync("Server stopped cleanly"); }

        Console.WriteLine("MAVLink listener stopped cleanly.");
    }
}
