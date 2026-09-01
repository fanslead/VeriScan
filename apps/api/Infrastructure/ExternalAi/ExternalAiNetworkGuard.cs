using System.Net;
using System.Net.Sockets;

namespace VeriScan.Infrastructure.ExternalAi;

internal sealed class ExternalAiNetworkPolicyException(string message) : Exception(message);

internal static class ExternalAiNetworkGuard
{
    public static SocketsHttpHandler CreateHandler(ExternalAiOptions options)
    {
        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromMilliseconds(options.ConnectTimeoutMs),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = (context, cancellationToken) =>
                ConnectAsync(context, options.ConnectTimeoutMs, cancellationToken)
        };
    }

    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        int maximumConnectTimeoutMs,
        CancellationToken cancellationToken)
    {
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestTimeout = context.InitialRequestMessage.Options.TryGetValue(
            ExternalAiProtocolSupport.ConnectTimeoutOption,
            out TimeSpan configuredTimeout)
            ? configuredTimeout
            : TimeSpan.FromMilliseconds(maximumConnectTimeoutMs);
        connectTimeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(
            (int)Math.Min(int.MaxValue, requestTimeout.TotalMilliseconds),
            100,
            Math.Max(100, maximumConnectTimeoutMs))));

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, connectTimeout.Token);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException("外部 AI 主机解析失败。", exception);
        }

        if (addresses.Length == 0 || addresses.Any(IsNonPublicAddress))
        {
            throw new ExternalAiNetworkPolicyException("外部 AI 主机解析到了不允许的网络地址。");
        }

        Exception? lastException = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, connectTimeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                lastException = exception;
                socket.Dispose();
                if (exception is OperationCanceledException)
                {
                    throw;
                }
            }
        }

        throw new HttpRequestException("外部 AI 主机连接失败。", lastException);
    }

    private static bool IsNonPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var first = bytes[0];
            var second = bytes[1];
            return first == 0 ||
                   first == 10 ||
                   (first == 100 && second is >= 64 and <= 127) ||
                   (first == 127) ||
                   (first == 169 && second == 254) ||
                   (first == 172 && second is >= 16 and <= 31) ||
                   (first == 192 && second == 168) ||
                   (first == 192 && second == 0 && bytes[2] == 0) ||
                   (first == 198 && second is 18 or 19) ||
                   first >= 224;
        }

        return address.IsIPv6LinkLocal ||
               address.IsIPv6SiteLocal ||
               (bytes[0] & 0xFE) == 0xFC ||
               address.Equals(IPAddress.IPv6Loopback) ||
               address.Equals(IPAddress.IPv6None) ||
               address.Equals(IPAddress.IPv6Any);
    }
}
