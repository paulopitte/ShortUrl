using System.Net;
using System.Net.Sockets;
using FluentValidation;
using ShortUrl.Application.ShortLinks.Commands.CreateShortLink;

namespace ShortUrl.Application.Validators;

/// <summary>
/// Validador FluentValidation para <see cref="CreateShortLinkCommand"/> garantindo URL pública válida (RN-02 / FR-002).
/// </summary>
public sealed class CreateShortLinkCommandValidator : AbstractValidator<CreateShortLinkCommand>
{
    public const int MaxTargetUrlLength = 2048;

    public CreateShortLinkCommandValidator()
    {
        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .WithMessage("A URL de destino é obrigatória.")
            .MaximumLength(MaxTargetUrlLength)
            .WithMessage($"A URL de destino deve ter no máximo {MaxTargetUrlLength} caracteres.")
            .Must(HasValidHttpOrHttpsScheme)
            .WithMessage("A URL de destino deve utilizar o protocolo http ou https.")
            .Must(HasValidPublicHost)
            .WithMessage("A URL de destino deve apontar para um endereço público válido (hosts locais ou IPs privados não são permitidos).");
    }

    private static bool HasValidHttpOrHttpsScheme(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true; // Tratado pelo NotEmpty
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasValidPublicHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true; // Tratado pelo NotEmpty
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = uri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (IsLocalHostname(host))
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return IsPublicIpAddress(ipAddress);
        }

        // Para nomes de domínio (DNS), exige ao menos um ponto separador de TLD válido
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.');
    }

    private static bool IsLocalHostname(string host)
    {
        var normalized = host.TrimEnd('.').ToLowerInvariant();

        return normalized == "localhost"
            || normalized.EndsWith(".localhost", StringComparison.Ordinal)
            || normalized.EndsWith(".local", StringComparison.Ordinal)
            || normalized.EndsWith(".internal", StringComparison.Ordinal);
    }

    private static bool IsPublicIpAddress(IPAddress ipAddress)
    {
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        if (IPAddress.IsLoopback(ipAddress) ||
            ipAddress.Equals(IPAddress.Any) ||
            ipAddress.Equals(IPAddress.Broadcast) ||
            ipAddress.Equals(IPAddress.IPv6Any) ||
            ipAddress.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ipAddress.GetAddressBytes();

            // 0.0.0.0/8 (Rede atual / não especificada)
            if (bytes[0] == 0)
            {
                return false;
            }

            // 10.0.0.0/8 (RFC 1918)
            if (bytes[0] == 10)
            {
                return false;
            }

            // 100.64.0.0/10 (Carrier-Grade NAT - RFC 6598)
            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            {
                return false;
            }

            // 127.0.0.0/8 (Loopback)
            if (bytes[0] == 127)
            {
                return false;
            }

            // 169.254.0.0/16 (Link-local - RFC 3927 / Cloud Metadata)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return false;
            }

            // 172.16.0.0/12 (RFC 1918)
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return false;
            }

            // 192.168.0.0/16 (RFC 1918)
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return false;
            }

            return true;
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || ipAddress.IsIPv6Multicast)
            {
                return false;
            }

            var bytes = ipAddress.GetAddressBytes();

            // fc00::/7 (Unique Local Address - RFC 4193)
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return false;
            }

            // fe80::/10 (Link-local IPv6)
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return false;
            }

            return true;
        }

        return false;
    }
}

