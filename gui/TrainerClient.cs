using System.IO.Pipes;
using System.Text;

namespace BalatroTrainer.Gui;

internal sealed class TrainerClient : IDisposable
{
    private const string PipeName = "BalatroTrainer";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _pipe is { IsConnected: true };

    public async Task<bool> ConnectAsync(int timeoutMs)
    {
        Disconnect();
        var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(timeoutMs);
        }
        catch (Exception)
        {
            pipe.Dispose();
            return false;
        }

        _pipe = pipe;
        _reader = new StreamReader(pipe, Encoding.ASCII);
        _writer = new StreamWriter(pipe, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
        return true;
    }

    public async Task<string?> SendAsync(string line)
    {
        if (!IsConnected || _reader is null || _writer is null) return null;

        await _gate.WaitAsync();
        try
        {
            using var cts = new CancellationTokenSource(2000);
            await _writer.WriteLineAsync(line.AsMemory(), cts.Token);
            return await _reader.ReadLineAsync(cts.Token);
        }
        catch (Exception)
        {
            Disconnect();
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Disconnect()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();
        _writer = null;
        _reader = null;
        _pipe = null;
    }

    public void Dispose()
    {
        Disconnect();
        _gate.Dispose();
    }
}
