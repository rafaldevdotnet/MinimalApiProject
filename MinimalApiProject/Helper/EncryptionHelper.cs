using System.Security.Cryptography;
using System.Text;

namespace MinimalApiProject.Helper
{
    /// <summary>
    /// Klasa do szyfrowania i deszyfrowania tekstu.
    /// </summary>
    public static class EncryptionHelper
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890ABCDEF");
        private static readonly byte[] IV = Encoding.UTF8.GetBytes("FEDCBA0987654321");
        private const string MarkerPrefix = "ENC{";
        private const string MarkerSuffix = "}";

        /// <summary>
        /// Szyfruje podany tekst.
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns>Zaszyfrowany ciąg znaków</returns>
        public static string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            var encryptor = aes.CreateEncryptor();

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            var base64 = Convert.ToBase64String(ms.ToArray());
            return $"{MarkerPrefix}{base64}{MarkerSuffix}";
        }

        /// <summary>
        /// Deszyfruje podany tekst, jeśli jest on zaszyfrowany.
        /// </summary>
        /// <param name="cipherText"></param>
        /// <returns>Odzyfrowany ciąg znaków</returns>
        public static string Decrypt(string cipherText)
        {
            if (!IsEncrypted(cipherText))
                throw new InvalidOperationException("Text is not encrypted");

            var base64 = cipherText[MarkerPrefix.Length..^MarkerSuffix.Length];
            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            var decryptor = aes.CreateDecryptor();

            using var ms = new MemoryStream(Convert.FromBase64String(base64));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Sprawdza, czy podany tekst jest zaszyfrowany.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsEncrypted(string value)
        {
            return value.StartsWith(MarkerPrefix) && value.EndsWith(MarkerSuffix);
        }
    }


}
