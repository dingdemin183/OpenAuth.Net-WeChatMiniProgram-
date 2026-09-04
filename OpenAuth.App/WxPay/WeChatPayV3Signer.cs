// OpenAuth.App/WxPay/WeChatPayV3Signer.cs

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信支付 V3 签名工具（支付和退款共用）
    /// </summary>
    public class WeChatPayV3Signer
    {
        private readonly RSA _rsaPrivateKey;
        private readonly string _mchId;
        private readonly string _serialNo;

        public WeChatPayV3Signer(string privateKeyPath, string mchId, string serialNo)
        {
            if (string.IsNullOrEmpty(privateKeyPath))
                throw new ArgumentException("私钥路径不能为空", nameof(privateKeyPath));
            if (string.IsNullOrEmpty(mchId))
                throw new ArgumentException("商户号不能为空", nameof(mchId));
            if (string.IsNullOrEmpty(serialNo))
                throw new ArgumentException("证书序列号不能为空", nameof(serialNo));

            _mchId = mchId;
            _serialNo = serialNo;
            _rsaPrivateKey = LoadPrivateKey(privateKeyPath);
        }

        /// <summary>
        /// 生成 Authorization 头
        /// </summary>
        public string GenerateAuthorization(string method, string url, string body, string nonceStr, string timestamp)
        {
            var signatureStr = BuildSignatureString(method, url, timestamp, nonceStr, body);
            var signature = Sign(signatureStr);

            return $"WECHATPAY2-SHA256-RSA2048 mchid=\"{_mchId}\",nonce_str=\"{nonceStr}\",signature=\"{signature}\",timestamp=\"{timestamp}\",serial_no=\"{_serialNo}\"";
        }

        /// <summary>
        /// 构建签名串
        /// </summary>
        private string BuildSignatureString(string method, string url, string timestamp, string nonceStr, string body)
        {
            return $"{method}\n{url}\n{timestamp}\n{nonceStr}\n{body}\n";
        }

        /// <summary>
        /// RSA SHA256 签名并 Base64 编码
        /// </summary>
        public string Sign(string signatureStr)
        {
            var data = Encoding.UTF8.GetBytes(signatureStr);
            var signedBytes = _rsaPrivateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signedBytes);
        }

        /// <summary>
        /// 生成随机字符串（32位）
        /// </summary>
        public static string GenerateNonceStr()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var result = new char[32];
            for (int i = 0; i < 32; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }

        /// <summary>
        /// 获取当前时间戳（秒）
        /// </summary>
        public static string GenerateTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        }

        /// <summary>
        /// 加载 PEM 私钥（支持 PKCS#8 和 RSA 格式）
        /// </summary>
        private RSA LoadPrivateKey(string pemPath)
        {
            try
            {
                if (!File.Exists(pemPath))
                {
                    // 尝试在 Cert 目录下查找
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    var fallbackPath = Path.Combine(basePath, "Cert", Path.GetFileName(pemPath));
                    if (File.Exists(fallbackPath))
                    {
                        pemPath = fallbackPath;
                    }
                    else
                    {
                        throw new FileNotFoundException($"私钥文件不存在: {pemPath}");
                    }
                }

                var pem = File.ReadAllText(pemPath);
                var privateKeyPem = pem
                    .Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                    .Replace("-----END RSA PRIVATE KEY-----", "")
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Trim();

                var privateKeyBytes = Convert.FromBase64String(privateKeyPem);
                var rsa = RSA.Create();

                // 尝试 PKCS#8 格式
                try
                {
                    rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);
                }
                catch
                {
                    // 如果不是 PKCS#8，尝试 RSAPrivateKey 格式
                    rsa.ImportRSAPrivateKey(privateKeyBytes, out _);
                }

                return rsa;
            }
            catch (Exception ex)
            {
                throw new Exception($"加载私钥失败: {ex.Message}", ex);
            }
        }
    }
}