using System.Net;
using System.Net.Sockets;

namespace SAVI.Infrastructure.Security;

public static class SsrfValidator
{
    public static bool IsUrlSafe(string urlString, out string? reason)
    {
        reason = null;

        if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
        {
            reason = "Invalid URL format.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            reason = $"Unsupported URL scheme: {uri.Scheme}. Only HTTP and HTTPS are permitted.";
            return false;
        }

        var host = uri.Host.Trim().ToLowerInvariant();

        if (host == "localhost" || host.EndsWith(".localhost") || host == "127.0.0.1" || host == "::1")
        {
            reason = "Access to localhost/loopback address is prohibited.";
            return false;
        }

        // Try IP parsing
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsPrivateOrRestrictedIp(ip))
            {
                reason = $"Access to private IP address {ip} is prohibited.";
                return false;
            }
        }
        else
        {
            // Resolve host to check IP addresses
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                foreach (var resolvedIp in addresses)
                {
                    if (IsPrivateOrRestrictedIp(resolvedIp))
                    {
                        reason = $"Host {host} resolved to restricted IP {resolvedIp}.";
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                // DNS resolution might fail offline or during validation
            }
        }

        return true;
    }

    private static bool IsPrivateOrRestrictedIp(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (Link Local / AWS metadata)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
            // 224.0.0.0/4 (Multicast)
            if (bytes[0] >= 224 && bytes[0] <= 239) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast || ip.IsIPv6SiteLocal) return true;
        }

        return false;
    }
}
