using System.Security.Cryptography;
using System.Text;

namespace MikroTek.Common
{
    public class Encryption
    {
        // ---------------------------------------------------------------- Passwords (one-way)
        // Passwords are HASHED, not encrypted: they can be checked but never turned back into text,
        // so even someone who opens the Users table cannot read anybody's password.

        private const int SaltSize = 16, HashSize = 32, Iterations = 100_000;

        /// <summary>Returns the value to store in Users.Password, e.g. "PBKDF2$100000$salt$hash".</summary>
        public static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>True when the typed password matches the stored hash.</summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations))
                return false;
            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- Data (two-way)
        // For values that must be read back later (e.g. a router password). AES-GCM with a key
        // made from encryptionKey; a fresh random nonce each time, so the same text never encrypts the same way.

        private const int NonceSize = 12, TagSize = 16;

        public static string Encrypt(string encryptionString, string encryptionKey)
        {
            try
            {
                var plain = Encoding.UTF8.GetBytes(encryptionString);
                var nonce = RandomNumberGenerator.GetBytes(NonceSize);
                var cipher = new byte[plain.Length];
                var tag = new byte[TagSize];

                using (var aes = new AesGcm(KeyFrom(encryptionKey), TagSize))
                    aes.Encrypt(nonce, plain, cipher, tag);

                return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
            }
            catch (Exception ex)
            {
                throw new Exception("Error encrypting data: " + ex.Message, ex);
            }
        }

        public static string Decrypt(string DecryptionString, string DecryptionKey)
        {
            try
            {
                var all = Convert.FromBase64String(DecryptionString);
                var nonce = all.AsSpan(0, NonceSize);
                var tag = all.AsSpan(NonceSize, TagSize);
                var cipher = all.AsSpan(NonceSize + TagSize);
                var plain = new byte[cipher.Length];

                using (var aes = new AesGcm(KeyFrom(DecryptionKey), TagSize))
                    aes.Decrypt(nonce, cipher, tag, plain); // throws if the key is wrong or the text was changed

                return Encoding.UTF8.GetString(plain);
            }
            catch (Exception ex)
            {
                throw new Exception("Error decrypting data: " + ex.Message, ex);
            }
        }

        private static byte[] KeyFrom(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));
    }
}
