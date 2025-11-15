using Atlas.Core.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    public sealed class CryptoService : ICryptoService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private readonly IMemoryCache _cache;

        // 使用线程安全的AES实例池
        private readonly ThreadLocal<Aes> _aesPool;

        public CryptoService(IMemoryCache cache, IOptions<CryptoOptions> options)
        {
            _cache = cache;

            // 初始化密钥
            var opts = options.Value;

            _key = PadKey(opts.Key, 32);
            _iv = PadKey(opts.IV, 16);

            // 初始化AES实例池
            _aesPool = new ThreadLocal<Aes>(() =>
            {
                var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                return aes;
            }, trackAllValues: false);
        }

        private byte[] PadKey(string key, int length)
        {
            var result = new byte[length];
            var keyBytes = Encoding.UTF8.GetBytes(key);
            Buffer.BlockCopy(keyBytes, 0, result, 0, Math.Min(keyBytes.Length, length));
            return result;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                // 检查缓存
                var cacheKey = $"enc_{plainText.GetHashCode()}";
                if (_cache.TryGetValue<string>(cacheKey, out var cached))
                    return cached;

                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes;

                // 使用线程本地的AES实例
                var aes = _aesPool.Value!;
                using (var encryptor = aes.CreateEncryptor())
                {
                    // 使用 TransformFinalBlock 一次性完成加密
                    cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                }

                // 转换为URL安全的Base64
                var result = Convert.ToBase64String(cipherBytes)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .TrimEnd('=');

                // 缓存结果
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));


                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Encryption failed", ex);
            }
        }

        public string? Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return null;

            try
            {
                // 检查缓存
                var cacheKey = $"dec_{cipherText.GetHashCode()}";
                if (_cache.TryGetValue<string>(cacheKey, out var cached))
                    return cached;

                // 还原Base64
                cipherText = cipherText.Replace('-', '+').Replace('_', '/');
                var padding = (4 - cipherText.Length % 4) % 4;
                if (padding > 0)
                    cipherText += new string('=', padding);

                var cipherBytes = Convert.FromBase64String(cipherText);
                byte[] plainBytes;

                // 使用线程本地的AES实例
                var aes = _aesPool.Value!;
                using (var decryptor = aes.CreateDecryptor())
                {
                    // 使用 TransformFinalBlock 一次性完成解密
                    plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                }

                var result = Encoding.UTF8.GetString(plainBytes);

                // 缓存结果
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));


                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        // 清理资源
        public void Dispose()
        {
            if (_aesPool.IsValueCreated)
            {
                _aesPool.Value?.Dispose();
            }
            _aesPool.Dispose();
        }
    }
    public class CryptoOptions
    {
        public string Key { get; set; } = string.Empty;
        public string IV { get; set; } = string.Empty;
    }
}
