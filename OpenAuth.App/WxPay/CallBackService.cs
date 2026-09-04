// OpenAuth.App/WxPay/CallBackService.cs

using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Warranty;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Enums;
using SqlSugar;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信回调服务，处理微信回调请求
    /// </summary>
    public class CallBackService : SqlSugarBaseApp<WarrantyRecord>
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuth _auth;
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly ILogger<CallBackService> _logger;

        public CallBackService(
            ISqlSugarClient db,
            IAuth auth,
            IOptions<AppSetting> appConfiguration,
            ILogger<CallBackService> logger) : base(db, auth)
        {
            _db = db;
            _auth = auth;
            _appConfiguration = appConfiguration;
            _logger = logger;
        }

        #region 验签

        /// <summary>
        /// 验证回调签名（V3）
        /// </summary>
        public bool VerifyCallbackSignature(
            string requestBody,
            string wechatpaySignature,
            string wechatpayTimestamp,
            string wechatpayNonce,
            string wechatpaySerial)
        {
            try
            {
                // 构建验签串
                var signStr = $"{wechatpayTimestamp}\n{wechatpayNonce}\n{requestBody}\n";

                // 获取微信支付平台证书
                var certificate = GetPlatformCertificate(wechatpaySerial);

                // 使用证书公钥验签
                using var rsa = certificate.GetRSAPublicKey();
                var data = Encoding.UTF8.GetBytes(signStr);
                var signature = Convert.FromBase64String(wechatpaySignature);

                var isValid = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                if (!isValid)
                {
                    _logger.LogWarning($"回调验签失败，serial_no: {wechatpaySerial}");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验签异常");
                return false;
            }
        }

        /// <summary>
        /// 获取微信支付平台证书
        /// </summary>
        private X509Certificate2 GetPlatformCertificate(string serialNo)
        {
            try
            {
                var config = _appConfiguration.Value.WeChatPay;
                if (config == null)
                    throw new InvalidOperationException("微信支付配置未找到");

                // 优先从配置的路径加载
                var certPath = config.PlatformCertPath;

                if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath))
                {
                    _logger.LogDebug("从配置路径加载平台证书: {CertPath}", certPath);
                    return new X509Certificate2(certPath);
                }


                // 尝试在 Certificates 目录下查找
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                var fallbackPath = Path.Combine(basePath, "Certificates", "wechatpay_platform_cert.pem");
                if (File.Exists(fallbackPath))
                {
                    _logger.LogDebug("从默认路径加载平台证书: {CertPath}", fallbackPath);
                    return new X509Certificate2(fallbackPath);
                }

                // 如果都没有，抛出异常
                throw new FileNotFoundException(
                    $"平台证书未找到，请检查配置。\n配置路径: {certPath}\n默认路径: {fallbackPath}\n证书序列号: {serialNo}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取平台证书失败，序列号: {SerialNo}", serialNo);
                throw;
            }
        }

        #endregion

        #region 解密

        /// <summary>
        /// 解密回调数据（V3）
        /// </summary>
        public PayCallbackData DecryptCallbackData(CallbackResource resource)
        {
            try
            {
                var config = _appConfiguration.Value.WeChatPay;
                var apiV3Key = config.ApiV3Key;

                if (string.IsNullOrEmpty(apiV3Key))
                    throw new Exception("API V3 密钥未配置");

                if (resource.Algorithm != "AEAD_AES_256_GCM")
                    throw new Exception($"不支持的加密算法: {resource.Algorithm}");

                // 解密
                var plaintext = AesGcmDecrypt(
                    apiV3Key,
                    resource.AssociatedData,
                    resource.Nonce,
                    resource.Ciphertext
                );

                // 解析 JSON
                var result = JsonSerializer.Deserialize<PayCallbackData>(plaintext, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解密回调数据失败");
                throw;
            }
        }

        /// <summary>
        /// AES-256-GCM 解密
        /// </summary>
        private string AesGcmDecrypt(string apiV3Key, string associatedData, string nonce, string ciphertext)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(apiV3Key);
                var nonceBytes = Encoding.UTF8.GetBytes(nonce);
                var ciphertextBytes = Convert.FromBase64String(ciphertext);
                var associatedDataBytes = Encoding.UTF8.GetBytes(associatedData);

                using var aes = new AesGcm(key);

                var plaintextBytes = new byte[ciphertextBytes.Length];

                aes.Decrypt(nonceBytes, ciphertextBytes, associatedDataBytes, plaintextBytes);

                return Encoding.UTF8.GetString(plaintextBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AES-GCM 解密失败");
                throw;
            }
        }

        #endregion

        #region 核心处理方法

        /// <summary>
        /// 处理支付回调通知（V3）
        /// </summary>
        public async Task<PayCallbackResult> HandlePayCallbackAsync(
            string requestBody,
            string wechatpaySignature,
            string wechatpayTimestamp,
            string wechatpayNonce,
            string wechatpaySerial)
        {
            var result = new PayCallbackResult();

            try
            {
                //  验签
                var isValid = VerifyCallbackSignature(
                    requestBody,
                    wechatpaySignature,
                    wechatpayTimestamp,
                    wechatpayNonce,
                    wechatpaySerial
                );

                if (!isValid)
                {
                    result.Success = false;
                    result.ErrorMessage = "签名验证失败";
                    return result;
                }

                // 解析回调通知
                var notification = JsonSerializer.Deserialize<CallbackNotification>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (notification == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "解析回调通知失败";
                    return result;
                }

                // 检查事件类型
                if (notification.EventType != "TRANSACTION.SUCCESS")
                {
                    result.Success = true;
                    result.Message = $"忽略事件类型: {notification.EventType}";
                    return result;
                }

                // 解密数据
                var payData = DecryptCallbackData(notification.Resource);

                if (payData == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "解密支付数据失败";
                    return result;
                }

                // 业务处理
                await ProcessPaymentAsync(payData);

                result.Success = true;
                result.PayData = payData;
                result.Message = "回调处理成功";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理支付回调失败");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 处理支付业务逻辑
        /// </summary>
        private async Task ProcessPaymentAsync(PayCallbackData payData)
        {
            try
            {
                var orderNo = payData.OutTradeNo;
                var transactionId = payData.TransactionId;
                var totalFee = payData.Amount?.Total ?? 0;

                _logger.LogInformation($"开始处理支付成功回调，订单号：{orderNo}，交易号：{transactionId}");

                // 查询订单
                var order = await _db.Queryable<WarrantyRecord>()
                    .Where(o => o.OrderNo == orderNo && !o.IsDeleted)
                    .FirstAsync()
                    .ConfigureAwait(false);

                if (order == null)
                {
                    _logger.LogWarning($"订单不存在：{orderNo}");
                    return;
                }

                // 防止重复处理
                if (order.OrderStatus == WarrantyStatusEnum.Paid)
                {
                    _logger.LogInformation($"订单已支付，忽略重复回调：{orderNo}");
                    return;
                }

                // 验证金额是否一致
                var expectedAmount = (int)(order.Amount * 100);
                if (expectedAmount != totalFee)
                {
                    _logger.LogError($"金额不一致：订单金额{expectedAmount}分，支付金额{totalFee}分，订单号：{orderNo}");
                    return;
                }

                // 更新订单支付状态
                order.OrderStatus = WarrantyStatusEnum.Paid;
                order.TransactionId = transactionId;
                order.PayTime = DateTime.Now;
                order.UpdateTime = DateTime.Now;

                await _db.Updateable(order)
                    .UpdateColumns(o => new { o.OrderStatus, o.TransactionId, o.PayTime, o.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                _logger.LogInformation($"订单支付成功，订单号：{orderNo}，交易号：{transactionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理支付业务逻辑失败");
                throw;
            }
        }

        #endregion

        #region 退款回调

        /// <summary>
        /// 处理退款回调通知（V3）
        /// </summary>
        public async Task<RefundCallbackResult> HandleRefundCallbackAsync(
            string requestBody,
            string wechatpaySignature,
            string wechatpayTimestamp,
            string wechatpayNonce,
            string wechatpaySerial)
        {
            var result = new RefundCallbackResult();

            try
            {
                // 验证签名
                var isValid = VerifyCallbackSignature(
                    requestBody,
                    wechatpaySignature,
                    wechatpayTimestamp,
                    wechatpayNonce,
                    wechatpaySerial
                );

                if (!isValid)
                {
                    result.Success = false;
                    result.ErrorMessage = "签名验证失败";
                    return result;
                }

                // 解析回调通知
                var notification = JsonSerializer.Deserialize<RefundCallbackNotification>(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (notification == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "解析退款回调通知失败";
                    return result;
                }

                // 检查事件类型
                if (notification.EventType != "REFUND.SUCCESS" &&
                    notification.EventType != "REFUND.ABNORMAL" &&
                    notification.EventType != "REFUND.CLOSED")
                {
                    result.Success = true;
                    result.Message = $"忽略事件类型: {notification.EventType}";
                    return result;
                }

                // 解密数据
                var refundData = DecryptRefundCallbackData(notification.Resource);

                if (refundData == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "解密退款回调数据失败";
                    return result;
                }

                // 业务处理
                await ProcessRefundAsync(refundData);

                result.Success = true;
                result.RefundData = refundData;
                result.Message = "退款回调处理成功";

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理退款回调失败");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// 解密退款回调数据（V3）
        /// </summary>
        public RefundCallbackData DecryptRefundCallbackData(CallbackResource resource)
        {
            try
            {
                var config = _appConfiguration.Value.WeChatPay;
                var apiV3Key = config.ApiV3Key;

                if (string.IsNullOrEmpty(apiV3Key))
                    throw new Exception("API V3 密钥未配置");

                if (resource.Algorithm != "AEAD_AES_256_GCM")
                    throw new Exception($"不支持的加密算法: {resource.Algorithm}");

                // 解密
                var plaintext = AesGcmDecrypt(
                    apiV3Key,
                    resource.AssociatedData,
                    resource.Nonce,
                    resource.Ciphertext
                );

                _logger.LogDebug($"退款回调解密后的数据: {plaintext}");

                // 解析 JSON
                var result = JsonSerializer.Deserialize<RefundCallbackData>(plaintext, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解密退款回调数据失败");
                throw;
            }
        }

        /// <summary>
        /// 处理退款业务逻辑
        /// </summary>
        private async Task ProcessRefundAsync(RefundCallbackData refundData)
        {
            try
            {
                var orderNo = refundData.OutTradeNo;
                var refundNo = refundData.OutRefundNo;
                var refundId = refundData.RefundId;
                var refundStatus = refundData.RefundStatus;
                var refundAmount = refundData.Amount?.Refund ?? 0;

                _logger.LogInformation($"开始处理退款回调，订单号：{orderNo}，退款单号：{refundNo}，状态：{refundStatus}");

                // 查询订单
                var order = await _db.Queryable<WarrantyRecord>()
                    .Where(o => o.OrderNo == orderNo && !o.IsDeleted)
                    .FirstAsync()
                    .ConfigureAwait(false);

                if (order == null)
                {
                    _logger.LogWarning($"订单不存在：{orderNo}");
                    return;
                }

                // 防止重复处理
                if (order.OrderStatus == WarrantyStatusEnum.Refunded)
                {
                    _logger.LogInformation($"订单已退款，忽略重复回调：{orderNo}");
                    return;
                }

                switch (refundStatus)
                {
                    case "SUCCESS":
                        // 退款成功
                        order.OrderStatus = WarrantyStatusEnum.Refunded;
                        order.RefundNo = refundNo;
                        order.RefundId = refundId;
                        order.AuditRemark = "退款成功";
                        order.UpdateTime = DateTime.Now;

                        await _db.Updateable(order)
                            .UpdateColumns(o => new
                            {
                                o.OrderStatus,
                                o.RefundNo,
                                o.RefundId,
                                o.AuditRemark,
                                o.UpdateTime
                            })
                            .ExecuteCommandAsync()
                            .ConfigureAwait(false);

                        _logger.LogInformation($"退款成功，订单号：{orderNo}，退款单号：{refundNo}");
                        break;

                    case "ABNORMAL":
                        // 退款异常
                        order.OrderStatus = WarrantyStatusEnum.RefundFailed;
                        order.AuditRemark = $"退款异常，微信退款单号：{refundId}";
                        order.UpdateTime = DateTime.Now;

                        await _db.Updateable(order)
                            .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                            .ExecuteCommandAsync()
                            .ConfigureAwait(false);

                        _logger.LogWarning($"退款异常，订单号：{orderNo}，退款单号：{refundNo}");
                        break;

                    case "CLOSED":
                        // 退款关闭
                        order.OrderStatus = WarrantyStatusEnum.RefundFailed;
                        order.AuditRemark = $"退款关闭，微信退款单号：{refundId}";
                        order.UpdateTime = DateTime.Now;

                        await _db.Updateable(order)
                            .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                            .ExecuteCommandAsync()
                            .ConfigureAwait(false);

                        _logger.LogWarning($"退款关闭，订单号：{orderNo}，退款单号：{refundNo}");
                        break;

                    default:
                        _logger.LogWarning($"未知退款状态：{refundStatus}，订单号：{orderNo}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理退款业务逻辑失败");
                throw;
            }
        }

        #endregion
    }
}