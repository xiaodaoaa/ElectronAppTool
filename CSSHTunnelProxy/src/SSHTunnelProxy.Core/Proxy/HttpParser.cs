using System.Text;

namespace SSHTunnelProxy.Core.Proxy;

/// <summary>
/// 轻量 HTTP 请求解析器，支持首期所需的 CONNECT 方法与头部解析。
/// </summary>
public static class HttpParser
{
    /// <summary>解析出的 HTTP 请求元数据。</summary>
    public readonly record struct HttpRequest(
        string Method,
        string RequestTarget,
        string Version,
        Dictionary<string, string> Headers)
    {
        /// <summary>CONNECT 请求的目标 host:port（若适用）。</summary>
        public string? ConnectTarget => Method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase)
            ? RequestTarget
            : null;
    }

    /// <summary>
    /// 从接收到的字节中解析出一个完整的 HTTP 请求头（请求行 + 头部）。
    /// 若数据不足（尚未读到空行）返回 null。
    /// </summary>
    public static HttpRequest? ParseHeaders(ReadOnlySpan<byte> data)
    {
        var text = Encoding.ASCII.GetString(data);
        var headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (headerEnd < 0)
            return null;

        var head = text[..headerEnd];
        var lines = head.Split("\r\n");

        if (lines.Length == 0)
            throw new HttpParseException("空的 HTTP 请求头。");

        // 请求行：METHOD SP 请求目标 SP HTTP/版本
        var requestLine = lines[0];
        var parts = requestLine.Split(' ');
        if (parts.Length < 3)
            throw new HttpParseException($"无效的请求行：{requestLine}");

        var method = parts[0];
        var target = parts[1];
        var version = parts[2];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0)
                continue;
            var name = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            headers[name] = value;
        }

        return new HttpRequest(method, target, version, headers);
    }

    /// <summary>
    /// 从 CONNECT 请求目标（host:port）解析出主机与端口。
    /// </summary>
    public static (string Host, int Port) ParseAuthority(string authority)
    {
        if (string.IsNullOrEmpty(authority))
            throw new HttpParseException("空的 CONNECT 目标。");

        // 处理形如 [IPv6]:port 或 host:port
        var idx = authority.LastIndexOf(':');
        if (idx <= 0 || idx == authority.Length - 1)
            throw new HttpParseException($"无效的 CONNECT 目标：{authority}");

        var host = authority[..idx].Trim('[', ']');
        if (!int.TryParse(authority[(idx + 1)..], out var port) || port is < 1 or > 65535)
            throw new HttpParseException($"无效端口：{authority[(idx + 1)..]}");

        return (host, port);
    }
}

/// <summary>HTTP 解析异常。</summary>
public sealed class HttpParseException : Exception
{
    public HttpParseException(string message) : base(message)
    {
    }
}
