using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataNro.Mang;

public enum ProxyType { None, Http, Socks4, Socks4a, Socks5 }

public class ProxyInfo
{
    public ProxyType Type { get; set; } = ProxyType.None;
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }

    public static ProxyInfo Http(string host, int port, string user = null, string pass = null)
        => new ProxyInfo { Type = ProxyType.Http, Host = host, Port = port, Username = user, Password = pass };
    public static ProxyInfo Socks4(string host, int port, string user = null)
        => new ProxyInfo { Type = ProxyType.Socks4, Host = host, Port = port, Username = user };
    public static ProxyInfo Socks4a(string host, int port, string user = null)
        => new ProxyInfo { Type = ProxyType.Socks4a, Host = host, Port = port, Username = user };
    public static ProxyInfo Socks5(string host, int port, string user = null, string pass = null)
        => new ProxyInfo { Type = ProxyType.Socks5, Host = host, Port = port, Username = user, Password = pass };

    public static ProxyInfo Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var u = new Uri(s.Contains("://") ? s : "http://" + s);
        var pi = new ProxyInfo { Host = u.Host, Port = u.Port <= 0 ? 1080 : u.Port };
        pi.Type = u.Scheme.ToLowerInvariant() switch
        {
            "http" => ProxyType.Http,
            "https" => ProxyType.Http,
            "socks4" => ProxyType.Socks4,
            "socks4a" => ProxyType.Socks4a,
            "socks" => ProxyType.Socks5,
            "socks5" => ProxyType.Socks5,
            _ => ProxyType.Http,
        };
        if (!string.IsNullOrEmpty(u.UserInfo))
        {
            var parts = u.UserInfo.Split(new[] { ':' }, 2);
            pi.Username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1) pi.Password = Uri.UnescapeDataString(parts[1]);
        }
        return pi;
    }

    public override string ToString() => $"{Type} {Host}:{Port}";
}

internal static class ProxyHandshake
{
    public static async Task ConnectThroughAsync(TcpClient client, ProxyInfo proxy, string targetHost, int targetPort, CancellationToken ct)
    {
        // The proxy socket itself must use the same deadline as the game socket.
        // Without the token a dead proxy can keep ConnectAsync blocked for minutes,
        // bypassing Session_ME.ConnectTimeoutMs entirely.
        await client.ConnectAsync(proxy.Host, proxy.Port, ct);
        var s = client.GetStream();
        switch (proxy.Type)
        {
            case ProxyType.Http: await HttpConnectAsync(s, targetHost, targetPort, proxy, ct); break;
            case ProxyType.Socks4: await Socks4Async(s, targetHost, targetPort, proxy, false, ct); break;
            case ProxyType.Socks4a: await Socks4Async(s, targetHost, targetPort, proxy, true, ct); break;
            case ProxyType.Socks5: await Socks5Async(s, targetHost, targetPort, proxy, ct); break;
            default: throw new NotSupportedException("proxy type " + proxy.Type);
        }
    }

    static async Task HttpConnectAsync(Stream s, string host, int port, ProxyInfo p, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append("CONNECT ").Append(host).Append(':').Append(port).Append(" HTTP/1.1\r\n");
        sb.Append("Host: ").Append(host).Append(':').Append(port).Append("\r\n");
        if (!string.IsNullOrEmpty(p.Username))
        {
            var cred = Convert.ToBase64String(Encoding.ASCII.GetBytes((p.Username ?? "") + ":" + (p.Password ?? "")));
            sb.Append("Proxy-Authorization: Basic ").Append(cred).Append("\r\n");
        }
        sb.Append("\r\n");
        byte[] req = Encoding.ASCII.GetBytes(sb.ToString());
        await s.WriteAsync(req, 0, req.Length, ct);
        await s.FlushAsync(ct);

        var resp = new StringBuilder();
        byte[] one = new byte[1];
        int matched = 0;
        while (matched < 4)
        {
            int n = await s.ReadAsync(one, 0, 1, ct);
            if (n <= 0) throw new IOException("proxy closed during CONNECT");
            char c = (char)one[0];
            resp.Append(c);
            if ((matched == 0 || matched == 2) && c == '\r') matched++;
            else if ((matched == 1 || matched == 3) && c == '\n') matched++;
            else matched = 0;
            if (resp.Length > 8192) throw new IOException("HTTP CONNECT header too large");
        }
        string line = resp.ToString();
        int sp = line.IndexOf(' ');
        if (sp < 0 || !int.TryParse(line.Substring(sp + 1, 3), out int code) || code != 200)
            throw new IOException("HTTP CONNECT failed: " + line.Split('\r')[0]);
    }

    static async Task Socks4Async(Stream s, string host, int port, ProxyInfo p, bool remoteResolve, CancellationToken ct)
    {
        byte[] ip = new byte[4];
        string hostname = null;
        if (remoteResolve)
        {
            ip[0] = 0; ip[1] = 0; ip[2] = 0; ip[3] = 1;
            hostname = host;
        }
        else
        {
            var addrs = await Dns.GetHostAddressesAsync(host, ct);
            IPAddress v4 = null;
            foreach (var a in addrs) if (a.AddressFamily == AddressFamily.InterNetwork) { v4 = a; break; }
            if (v4 == null) throw new IOException("SOCKS4 requires IPv4 (or use socks4a)");
            ip = v4.GetAddressBytes();
        }
        byte[] user = string.IsNullOrEmpty(p.Username) ? Array.Empty<byte>() : Encoding.ASCII.GetBytes(p.Username);
        var ms = new MemoryStream();
        ms.WriteByte(0x04); ms.WriteByte(0x01);
        ms.WriteByte((byte)(port >> 8)); ms.WriteByte((byte)(port & 0xFF));
        ms.Write(ip, 0, 4);
        ms.Write(user, 0, user.Length); ms.WriteByte(0);
        if (hostname != null)
        {
            byte[] h = Encoding.ASCII.GetBytes(hostname);
            ms.Write(h, 0, h.Length); ms.WriteByte(0);
        }
        byte[] req = ms.ToArray();
        await s.WriteAsync(req, 0, req.Length, ct);
        await s.FlushAsync(ct);

        byte[] resp = new byte[8];
        await ReadExactAsync(s, resp, 8, ct);
        if (resp[0] != 0x00 || resp[1] != 0x5A) throw new IOException("SOCKS4 refused code=0x" + resp[1].ToString("X2"));
    }

    static async Task Socks5Async(Stream s, string host, int port, ProxyInfo p, CancellationToken ct)
    {
        bool hasAuth = !string.IsNullOrEmpty(p.Username);
        byte[] greeting = hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
        await s.WriteAsync(greeting, 0, greeting.Length, ct);
        await s.FlushAsync(ct);

        byte[] sel = new byte[2];
        await ReadExactAsync(s, sel, 2, ct);
        if (sel[0] != 0x05) throw new IOException("SOCKS5 bad version");
        if (sel[1] == 0xFF) throw new IOException("SOCKS5 no acceptable auth");

        if (sel[1] == 0x02)
        {
            byte[] u = Encoding.ASCII.GetBytes(p.Username ?? "");
            byte[] pw = Encoding.ASCII.GetBytes(p.Password ?? "");
            var ms = new MemoryStream();
            ms.WriteByte(0x01);
            ms.WriteByte((byte)u.Length); ms.Write(u, 0, u.Length);
            ms.WriteByte((byte)pw.Length); ms.Write(pw, 0, pw.Length);
            byte[] auth = ms.ToArray();
            await s.WriteAsync(auth, 0, auth.Length, ct);
            await s.FlushAsync(ct);
            byte[] ar = new byte[2];
            await ReadExactAsync(s, ar, 2, ct);
            if (ar[1] != 0x00) throw new IOException("SOCKS5 auth failed");
        }

        var req = new MemoryStream();
        req.WriteByte(0x05); req.WriteByte(0x01); req.WriteByte(0x00);
        if (IPAddress.TryParse(host, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                req.WriteByte(0x01); var b = ip.GetAddressBytes(); req.Write(b, 0, b.Length);
            }
            else
            {
                req.WriteByte(0x04); var b = ip.GetAddressBytes(); req.Write(b, 0, b.Length);
            }
        }
        else
        {
            byte[] h = Encoding.ASCII.GetBytes(host);
            if (h.Length > 255) throw new IOException("hostname too long");
            req.WriteByte(0x03); req.WriteByte((byte)h.Length); req.Write(h, 0, h.Length);
        }
        req.WriteByte((byte)(port >> 8)); req.WriteByte((byte)(port & 0xFF));
        byte[] rb = req.ToArray();
        await s.WriteAsync(rb, 0, rb.Length, ct);
        await s.FlushAsync(ct);

        byte[] head = new byte[4];
        await ReadExactAsync(s, head, 4, ct);
        if (head[0] != 0x05) throw new IOException("SOCKS5 bad reply version");
        if (head[1] != 0x00) throw new IOException("SOCKS5 connect failed rep=0x" + head[1].ToString("X2"));
        int skip;
        switch (head[3])
        {
            case 0x01: skip = 4; break;
            case 0x04: skip = 16; break;
            case 0x03:
                byte[] one = new byte[1]; await ReadExactAsync(s, one, 1, ct); skip = one[0]; break;
            default: throw new IOException("SOCKS5 unknown atyp=" + head[3]);
        }
        byte[] rest = new byte[skip + 2];
        await ReadExactAsync(s, rest, rest.Length, ct);
    }

    static async Task ReadExactAsync(Stream s, byte[] buf, int len, CancellationToken ct)
    {
        int off = 0;
        while (off < len)
        {
            int n = await s.ReadAsync(buf, off, len - off, ct);
            if (n <= 0) throw new IOException("proxy stream closed");
            off += n;
        }
    }
}
