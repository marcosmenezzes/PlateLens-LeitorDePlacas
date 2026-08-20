using System.Net;

namespace PlateLens.Domain.Rules;

/// <summary>Protege o cadastro de câmeras aceitando somente endereços IPv4 de redes privadas.</summary>
public static class CameraNetworkPolicy
{
    /// <summary>Valida e normaliza um IPv4 que pertença às faixas privadas RFC 1918.</summary>
    public static bool TryNormalizePrivateIpv4(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (!IPAddress.TryParse(value, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        var bytes = address.GetAddressBytes();
        var isPrivate = bytes[0] == 10 ||
                        bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                        bytes[0] == 192 && bytes[1] == 168;
        if (!isPrivate) return false;
        normalized = address.ToString();
        return true;
    }
}
