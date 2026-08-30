using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace AI_Workshop.Services;

public sealed class AttendanceTokenService(IDataProtectionProvider dataProtectionProvider)
{
    private readonly ITimeLimitedDataProtector _qrProtector = dataProtectionProvider
        .CreateProtector("Attendly.Attendance.QrToken.v1")
        .ToTimeLimitedDataProtector();
    private readonly IDataProtector _fallbackProtector = dataProtectionProvider
        .CreateProtector("Attendly.Attendance.FallbackCode.v1");

    public string CreateQrToken(int sessionId, TimeSpan lifetime)
    {
        var payload = JsonSerializer.Serialize(new QrPayload(sessionId, Guid.NewGuid()));
        return _qrProtector.Protect(payload, lifetime);
    }

    public bool TryReadQrToken(string token, out int sessionId)
    {
        sessionId = 0;
        try
        {
            var payload = JsonSerializer.Deserialize<QrPayload>(_qrProtector.Unprotect(token));
            if (payload is null || payload.SessionId <= 0) return false;
            sessionId = payload.SessionId;
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            return false;
        }
    }

    public string ProtectFallbackCode(string code) => _fallbackProtector.Protect(code);

    public string? TryReadFallbackCode(string? protectedCode)
    {
        if (string.IsNullOrWhiteSpace(protectedCode)) return null;
        try { return _fallbackProtector.Unprotect(protectedCode); }
        catch (CryptographicException) { return null; }
    }

    private sealed record QrPayload(int SessionId, Guid Nonce);
}
