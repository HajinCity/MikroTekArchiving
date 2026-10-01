using System;
using System.Collections.Generic;
using System.Text;

namespace MikroTek.Common
{
    public class Encryption
    {
        private static byte[] keys = { 0x12, 0x83, 0x3F, 0x41, 0x52, 0x6E, 0x7C, 0x8A };
        public static string Encrypt(string encryptionString, string encryptionKey)
        {
            try
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(encryptionString);
                for (int i = 0; i < inputBytes.Length; i++)
                {
                    inputBytes[i] ^= keys[i % keys.Length];
                }
                return Convert.ToBase64String(inputBytes);

            }
            catch (Exception ex)
            {
                throw new Exception("Error encrypting data: " + ex.Message);
            }
        }
        public static string Decrypt(string DecryptionString, string DecryptionKey)
        {
            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(DecryptionString);
                byte[] decryptedBytes = new byte[encryptedBytes.Length];
                for (int i = 0; i < encryptedBytes.Length; i++)
                {
                    decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keys[i % keys.Length]);
                }
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (Exception ex)
            {
                throw new Exception("Error decrypting data: " + ex.Message);
            }
        }



    }
}
