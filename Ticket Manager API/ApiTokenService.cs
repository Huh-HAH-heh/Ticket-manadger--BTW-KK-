using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ticket_manadger
{
    public sealed class ApiTokenService
    {
        private const int TokenLifetimeHours = 8;

        public string Create(User user)
        {
            var claims = new ApiTokenClaims(
                user.Id, user.Login ?? string.Empty, user.Role,
                DateTimeOffset.UtcNow.AddHours(TokenLifetimeHours).ToUnixTimeSeconds());

            var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
            var signature = Sign(payload);
            return $"{payload}.{Base64UrlEncode(signature)}";
        }

        public bool TryValidate(string token, out ApiTokenClaims claims)
        {
            claims = default!;
            var parts = token.Split('.');
            if (parts.Length != 2) return false;

            byte[] suppliedSignature;
            byte[] payloadBytes;
            try
            {
                suppliedSignature = Base64UrlDecode(parts[1]);
                payloadBytes = Base64UrlDecode(parts[0]);
            }
            catch (FormatException) { return false; }

            var expectedSignature = Sign(parts[0]);
            if (!CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
                return false;

            try
            {
                var parsed = JsonSerializer.Deserialize<ApiTokenClaims>(payloadBytes);
                if (parsed is null || parsed.UserId <= 0 || string.IsNullOrWhiteSpace(parsed.Role) ||
                    parsed.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    return false;

                claims = parsed;
                return true;
            }
            catch (JsonException) { return false; }
        }

        private static byte[] Sign(string payload)
        {
            var secret = Environment.GetEnvironmentVariable("TICKET_API_TOKEN_SECRET");
            if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
                throw new InvalidOperationException(
                    "TICKET_API_TOKEN_SECRET must contain at least 32 UTF-8 bytes.");
            return HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Base64UrlDecode(string value)
        {
            value = value.Replace('-', '+').Replace('_', '/');
            value += value.Length % 4 switch { 0 => "", 2 => "==", 3 => "=", _ => throw new FormatException() };
            return Convert.FromBase64String(value);
        }
    }

    public sealed record ApiTokenClaims(int UserId, string Login, string Role, long ExpiresAt);
}
