using System.Net;
using System.Text;

namespace MavlinkDeviceServer.Logging;

public sealed class DebugLog : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _sync = new();

    public DebugLog(string path)
    {
        _writer = new StreamWriter(path, append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false
        };
    }

    public void Write(string message)
    {
        lock (_sync)
        {
            _writer.WriteLine($"{DateTimeOffset.Now:O} {message}");
        }
    }

    public void WriteFrame(string direction, IPEndPoint endpoint, string description, ReadOnlySpan<byte> frame)
    {
        Write($"{direction} {endpoint} {description} LEN={frame.Length} FRAME={Convert.ToHexString(frame)}");
    }

    public Task WriteAsync(string message)
    {
        Write(message);
        return Task.CompletedTask;
    }

    public Task WriteFrameAsync(string direction, IPEndPoint endpoint, string description, byte[] frame)
    {
        WriteFrame(direction, endpoint, description, frame);
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
