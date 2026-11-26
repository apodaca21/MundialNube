using System.Net.Sockets;
using System.Text;

namespace TournamentServices.Api.Messaging;

public sealed class MessagingOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 61613;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Destination { get; set; } = "/queue/champions";
}

/// <summary>STOMP 1.2 transport for persistent Artemis queues; bounded frames and explicit acknowledgements.</summary>
public sealed class StompConnection : IAsyncDisposable
{
    private readonly TcpClient _client = new();
    private NetworkStream Stream => _client.GetStream();
    private readonly byte[] _one = new byte[1];
    public async Task ConnectAsync(MessagingOptions options, CancellationToken ct)
    {
        await _client.ConnectAsync(options.Host, options.Port, ct);
        await SendAsync("CONNECT", new()
        {
            ["accept-version"] = "1.2",
            ["host"] = options.Host,
            ["login"] = options.Username,
            ["passcode"] = options.Password,
            ["heart-beat"] = "0,0"
        }, "", ct);
        var response = await ReadAsync(ct);
        if (response.Command != "CONNECTED") throw new IOException("Broker refused STOMP connection.");
    }
    public async Task SendAsync(string command, Dictionary<string, string> headers, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var builder = new StringBuilder(command + "\n");
        foreach (var (key, value) in headers)
            builder.Append(Escape(key)).Append(':').Append(command == "CONNECT" ? CleanConnect(value) : Escape(value)).Append('\n');
        builder.Append("content-length:").Append(bytes.Length).Append("\n\n");
        await Stream.WriteAsync(Encoding.UTF8.GetBytes(builder.ToString()), ct);
        await Stream.WriteAsync(bytes, ct);
        await Stream.WriteAsync(new byte[] { 0 }, ct);
        await Stream.FlushAsync(ct);
    }
    public async Task<StompFrame> ReadAsync(CancellationToken ct)
    {
        string command;
        do { command = await LineAsync(ct); } while (command.Length == 0);
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            var line = await LineAsync(ct); if (line.Length == 0) break;
            var colon = line.IndexOf(':'); if (colon < 1) throw new IOException("Malformed STOMP header.");
            headers.TryAdd(Unescape(line[..colon]), Unescape(line[(colon + 1)..]));
            if (headers.Count > 64) throw new IOException("Too many STOMP headers.");
        }
        byte[] body;
        if (headers.TryGetValue("content-length", out var contentLength))
        {
            if (!int.TryParse(contentLength, out var length) || length < 0 || length > 1_048_576) throw new IOException("Invalid STOMP content length.");
            body = new byte[length]; await Stream.ReadExactlyAsync(body, ct);
            if (await ByteAsync(ct) != 0) throw new IOException("Missing STOMP frame terminator.");
        }
        else
        {
            using var buffer = new MemoryStream(); int next;
            while ((next = await ByteAsync(ct)) != 0) { buffer.WriteByte((byte)next); if (buffer.Length > 1_048_576) throw new IOException("STOMP frame too large."); }
            body = buffer.ToArray();
        }
        if (command == "ERROR") throw new IOException("Broker reported a STOMP error.");
        return new(command, headers, Encoding.UTF8.GetString(body));
    }
    private async Task<string> LineAsync(CancellationToken ct)
    {
        using var buffer = new MemoryStream(); int next;
        while ((next = await ByteAsync(ct)) != 10) { if (next != 13) buffer.WriteByte((byte)next); if (buffer.Length > 8192) throw new IOException("STOMP line too long."); }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
    private async Task<int> ByteAsync(CancellationToken ct)
    {
        if (await Stream.ReadAsync(_one, ct) == 0) throw new EndOfStreamException("Broker disconnected.");
        return _one[0];
    }
    private static string CleanConnect(string value) => value.IndexOfAny(['\n', '\r', '\0']) >= 0 ? throw new ArgumentException("Invalid broker credential.") : value;
    private static string Escape(string v) => v.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace(":", "\\c");
    private static string Unescape(string v)
    {
        var b = new StringBuilder();
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i] != '\\') { b.Append(v[i]); continue; }
            if (++i == v.Length) throw new IOException("Invalid STOMP escape.");
            b.Append(v[i] switch { 'n' => '\n', 'r' => '\r', 'c' => ':', '\\' => '\\', _ => throw new IOException("Invalid STOMP escape.") });
        }
        return b.ToString();
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.Connected)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await SendAsync("DISCONNECT", new(), "", timeout.Token);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or OperationCanceledException) { }
        _client.Dispose();
    }
}
public sealed record StompFrame(string Command, Dictionary<string, string> Headers, string Body);
