using Atlas.Core.Security;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Security
{
    public sealed class CryptoService : ICryptoService, IDisposable
    {
        private readonly byte[] _key;
        private bool _disposed;

        public CryptoService(IOptions<CryptoOptions> options)
        {
            ArgumentException.ThrowIfNullOrEmpty(options.Value.Key, nameof(options.Value.Key));

            // 使用SHA256派生固定长度密钥
            _key = DeriveKey(options.Value.Key, 32);
        }

        private static byte[] DeriveKey(string key, int length)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);

            // 如果密钥长度正好，直接返回
            if (keyBytes.Length == length)
                return keyBytes;

            // 使用SHA256派生密钥
            var hash = SHA256.HashData(keyBytes); // ✅ .NET 8 静态方法，无需using

            if (hash.Length >= length)
                return hash.AsSpan(0, length).ToArray();

            // 如果需要更长的密钥，使用PBKDF2
            return Rfc2898DeriveBytes.Pbkdf2(
                keyBytes,
                "Atlas.Security.Salt"u8.ToArray(), // ✅ .NET 8 UTF-8 字符串字面量
                10000,
                HashAlgorithmName.SHA256,
                length);
        }

        public string Encrypt(string plainText)
        {
            ArgumentException.ThrowIfNullOrEmpty(plainText);
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                using var aes = Aes.Create();
                aes.Key = _key;
                aes.GenerateIV(); // ✅ 随机IV
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var plainBytes = Encoding.UTF8.GetBytes(plainText);

                using var encryptor = aes.CreateEncryptor();
                var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                // ✅ 格式: IV(16字节) + 密文
                var result = new byte[aes.IV.Length + cipherBytes.Length];
                aes.IV.CopyTo(result, 0);
                cipherBytes.CopyTo(result, aes.IV.Length);

                // 转换为URL安全的Base64
                return Convert.ToBase64String(result)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Encryption failed", ex);
            }
        }

        public string? Decrypt(string cipherText)
        {
            ArgumentException.ThrowIfNullOrEmpty(cipherText);
            ObjectDisposedException.ThrowIf(_disposed, this);

            try
            {
                // 还原Base64
                var base64 = cipherText
                    .Replace('-', '+')
                    .Replace('_', '/')
                    .PadRight(cipherText.Length + (4 - cipherText.Length % 4) % 4, '=');

                var data = Convert.FromBase64String(base64);

                // 至少需要16字节IV
                if (data.Length < 16)
                    return null;

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // ✅ 提取IV (前16字节) - 使用 Span
                aes.IV = data.AsSpan(0, 16).ToArray();

                // ✅ 提取密文
                var cipherBytes = data.AsSpan(16).ToArray();

                using var decryptor = aes.CreateDecryptor();
                var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // ✅ .NET 8: CryptographicOperations.ZeroMemory 更安全
            CryptographicOperations.ZeroMemory(_key);

            _disposed = true;
        }
    }

    public class CryptoOptions
    {
        public required string Key { get; set; } // ✅ .NET 8 required 属性
    }
}