using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Keemya.Frontend.Stores
{
    public static class SessionManager
    {
        private static readonly string SessionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session.txt");

        public static void SaveSession(string username, string role)
        {
            try
            {
                string plainText = $"{username}:{role}";
                string cipherText = Encrypt(plainText);
                File.WriteAllText(SessionFilePath, cipherText);
            }
            catch { }
        }

        public static (string? Username, string? Role) GetSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    string cipherText = File.ReadAllText(SessionFilePath).Trim();
                    if (!string.IsNullOrEmpty(cipherText))
                    {
                        string plainText = Decrypt(cipherText);
                        var parts = plainText.Split(':');
                        if (parts.Length >= 2)
                        {
                            return (parts[0], parts[1]);
                        }
                        return (parts[0], "Admin");
                    }
                }
            }
            catch 
            {
                // Clear the session file if it's corrupted or decryption fails
                ClearSession();
            }
            return (null, null);
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch { }
        }

        private static byte[] GetEncryptionKey()
        {
            string machineId = Keemya.Frontend.Services.LicenseService.GetMachineId();
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId));
            }
        }

        private static string Encrypt(string plainText)
        {
            byte[] key = GetEncryptionKey();
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream())
                {
                    // Write IV first to the stream
                    ms.Write(iv, 0, iv.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs, Encoding.UTF8))
                    {
                        sw.Write(plainText);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static string Decrypt(string cipherText)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            byte[] key = GetEncryptionKey();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                byte[] iv = new byte[aes.BlockSize / 8];
                byte[] cipher = new byte[fullCipher.Length - iv.Length];

                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }
}
