using System.Security.Cryptography;

namespace Ticket_manadger
{
    internal static class PasswordHasher
    {
        private const int Iterations = 210_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const string Algorithm = "pbkdf2-sha256";

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return string.Join("$", new[]
            {
                Algorithm,
                Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash)
            });
        }

        public static bool Verify(string password, string? encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return false;
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != Algorithm ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000 || iterations > 2_000_000)
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                if (salt.Length < SaltSize || expected.Length != HashSize) return false;
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) { return false; }
        }
    }
}
