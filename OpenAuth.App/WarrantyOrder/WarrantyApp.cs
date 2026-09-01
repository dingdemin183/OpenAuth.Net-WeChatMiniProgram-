using Infrastructure;
using Microsoft.Extensions.Options;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.WxPay;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenAuth.App.Warranty
{
    public class WarrantyApp : SqlSugarBaseApp<WarrantyCard>
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuth _auth;
        private readonly WxPayService _wxPayService;
        private readonly IOptions<AppSetting> _appConfiguration;

        public WarrantyApp(
            ISqlSugarClient db,
            IAuth auth,
            WxPayService wxPayService,
            IOptions<AppSetting> appConfiguration) : base(db, auth)
        {
            _db = db;
            _auth = auth;
            _wxPayService = wxPayService;
            _appConfiguration = appConfiguration;
        }

        #region 订单相关

        /// <summary>
        /// 创建延保订单并返回支付参数（小程序端调用）
        /// </summary>
        public async Task<WeChatPayResultResp> CreatePayOrderAsync(CreateWarrantyPayOrderReq req)
        {
            // 参数校验
            if (req == null)
                throw new Exception("请求参数不能为空");
            if (req.WarrantyYears < 1 || req.WarrantyYears > 3)
                throw new Exception("延保年限必须为1-3年");
            if (req.Amount <= 0)
                throw new Exception("支付金额必须大于0");

            var user = _auth.GetCurrentSession();
            if (user?.UserId == null)
            {
                throw new Exception("登录信息错误，请重新登录");
            }

            // 获取用户openid（从第三方认证表获取）
            var externalAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == user.UserId);

            if (externalAuth == null || string.IsNullOrEmpty(externalAuth.OpenId))
            {
                throw new Exception("未获取到微信OpenId，请重新登录");
            }

            // 生成订单号并保存订单
            var orderNo = GenerateOrderNo();
            var order = new WarrantyOrder
            {
                Id = Guid.NewGuid().ToString("N"),
                OrderNo = orderNo,
                UserId = user.UserId,
                UserName = req.UserName,
                Phone = req.Phone, 
                ProductName = req.ProductName,
                ProductBrand = req.ProductBrand,
                ProductType = req.ProductType,
                ProductModel = req.ProductModel,
                PurchaseDate = req.PurchaseDate,
                EnergyImage = req.EnergyImage,
                ProductImage = req.ProductImage,
                TradeImage = req.TradeImage,
                WarrantyYears = req.WarrantyYears,
                Amount = req.Amount,
                PayStatus = 0, // 待支付
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            await _db.Insertable(order).ExecuteCommandAsync();

            // 调用微信统一下单
            var config = _appConfiguration.Value.WeChatPay;
            var unifiedReq = new WeChatPayUnifiedOrderReq
            {
                AppId = config.AppId,
                MchId = config.MchId,
                Body = $"延保服务-{req.ProductName}",
                OutTradeNo = orderNo,
                TotalFee = (int)(req.Amount * 100), // 元转分
                SpbillCreateIp = "127.0.0.1", // 实际应获取用户IP
                NotifyUrl = config.NotifyUrl,
                TradeType = "JSAPI",
                OpenId = externalAuth.OpenId
            };

            var payResult = await _wxPayService.UnifiedOrderAsync(unifiedReq);

            // 保存预支付ID
            order.PrepayId = payResult.Package?.Replace("prepay_id=", "");
            await _db.Updateable(order)
                .UpdateColumns(x => new { x.PrepayId })
                .ExecuteCommandAsync();

            // 返回支付参数给前端
            payResult.OrderNo = orderNo;
            return payResult;
        }

        /// <summary>
        /// 支付回调处理
        /// </summary>
        public async Task<bool> HandlePayCallbackAsync(SortedDictionary<string, string> callbackData)
        {
            var config = _appConfiguration.Value.WeChatPay;

            //  验证签名
            if (!_wxPayService.VerifyCallbackSign(callbackData, config.ApiKey))
            {
                throw new Exception("签名验证失败");
            }

            // 验证支付结果
            if (callbackData["return_code"] != "SUCCESS" || callbackData["result_code"] != "SUCCESS")
            {
                throw new Exception($"支付失败：{callbackData.GetValueOrDefault("err_code_des", "未知错误")}");
            }

            //  获取订单号
            var orderNo = callbackData["out_trade_no"];
            var transactionId = callbackData["transaction_id"];
            var totalFee = int.Parse(callbackData["total_fee"]);

            // 查询订单
            var order = await _db.Queryable<WarrantyOrder>()
                .Where(o => o.OrderNo == orderNo && !o.IsDeleted)
                .FirstAsync();

            if (order == null)
                throw new Exception($"订单不存在：{orderNo}");

            if (order.PayStatus == 1)
            {
                // 已支付，防止重复处理
                return true;
            }

            // 验证金额是否一致
            if ((int)(order.Amount * 100) != totalFee)
            {
                throw new Exception($"金额不一致：订单金额{order.Amount}，支付金额{totalFee / 100.0}");
            }

            // 更新订单支付状态
            order.PayStatus = 1;
            order.PayTime = DateTime.Now;
            order.TransactionId = transactionId;
            order.CallbackData = Newtonsoft.Json.JsonConvert.SerializeObject(callbackData);
            order.CallbackTime = DateTime.Now;
            order.UpdateTime = DateTime.Now;

            await _db.Updateable(order)
                .UpdateColumns(o => new { o.PayStatus, o.PayTime, o.TransactionId, o.CallbackData, o.CallbackTime, o.UpdateTime })
                .ExecuteCommandAsync();

            // 7. 创建延保卡
            await CreateWarrantyCardAsync(order);

            return true;
        }

        /// <summary>
        /// 查询订单支付状态（小程序端轮询）
        /// </summary>
        public async Task<WarrantyOrderResp> QueryOrderStatusAsync(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                throw new Exception("订单号不能为空");

            var order = await _db.Queryable<WarrantyOrder>()
                .Where(o => o.OrderNo == orderNo && !o.IsDeleted)
                .FirstAsync();

            if (order == null)
                throw new Exception("订单不存在");

            return new WarrantyOrderResp
            {
                Id = order.Id,
                OrderNo = order.OrderNo,
                PayStatus = order.PayStatus,
                PayStatusName = GetPayStatusName(order.PayStatus),
                PayTime = order.PayTime,
                TransactionId = order.TransactionId,
                Amount = order.Amount,
                CreateTime = order.CreateTime
            };
        }

        #endregion

        #region 延保卡相关

        /// <summary>
        /// 创建延保卡（支付成功后调用）
        /// </summary>
        private async Task<WarrantyCardResp> CreateWarrantyCardAsync(WarrantyOrder order)
        {
            var now = DateTime.Now;
            var endDate = now.AddYears(order.WarrantyYears);

            var card = new WarrantyCard
            {
                Id = Guid.NewGuid().ToString("N"),
                CardNo = GenerateCardNo(),
                UserId = order.UserId,
                OrderId = order.Id,
                UserName = order.UserName,
                Phone = order.Phone,
                ProductName = order.ProductName,
                ProductBrand = order.ProductBrand,
                ProductType = order.ProductType,
                ProductModel = order.ProductModel,
                PurchaseDate = order.PurchaseDate,
                WarrantyYears = order.WarrantyYears,
                PaidAmount = order.Amount,
                StartDate = now,
                EndDate = endDate,
                CardStatus = 1, // 生效中
                IsRenewal = false,
                CreateTime = now,
                IsDeleted = false
            };

            await _db.Insertable(card).ExecuteCommandAsync().ConfigureAwait(false);

            return await GetCardDetailAsync(card.Id);
        }

        /// <summary>
        /// 获取延保卡详情
        /// </summary>
        public async Task<WarrantyCardResp> GetCardDetailAsync(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new Exception("延保卡ID不能为空");

            var card = await _db.Queryable<WarrantyCard>()
                .Where(c => c.Id == cardId && !c.IsDeleted)
                .FirstAsync();

            if (card == null)
                throw new Exception("延保卡不存在");

            return MapToCardResp(card);
        }

        /// <summary>
        /// 获取用户的延保卡列表（小程序端）
        /// </summary>
        public async Task<List<WarrantyCardResp>> GetUserCardsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new Exception("用户ID不能为空");

            var cards = await _db.Queryable<WarrantyCard>()
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.CreateTime)
                .ToListAsync()
                .ConfigureAwait(false);

            return cards.Select(c => MapToCardResp(c)).ToList();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 生成订单号：WB + 日期(8位) + 4位随机数
        /// </summary>
        private string GenerateOrderNo()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999).ToString();
            return $"WB{datePart}{random}";
        }

        /// <summary>
        /// 生成延保卡号：YC + 日期(8位) + 4位随机数
        /// </summary>
        private string GenerateCardNo()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999).ToString();
            return $"YC{datePart}{random}";
        }

        /// <summary>
        /// 获取支付状态名称
        /// </summary>
        private string GetPayStatusName(int status)
        {
            return status switch
            {
                0 => "待支付",
                1 => "已支付",
                2 => "已取消",
                3 => "已退款",
                _ => "未知"
            };
        }

        /// <summary>
        /// 获取延保卡状态名称
        /// </summary>
        private string GetCardStatusName(int status)
        {
            return status switch
            {
                0 => "待生效",
                1 => "生效中",
                2 => "已过期",
                3 => "已退款",
                _ => "未知"
            };
        }

        /// <summary>
        /// 映射延保卡实体到响应
        /// </summary>
        private WarrantyCardResp MapToCardResp(WarrantyCard card)
        {
            var now = DateTime.Now;
            var isExpired = card.EndDate < now;
            var remainingDays = isExpired ? 0 : (int)(card.EndDate - now).TotalDays;

            return new WarrantyCardResp
            {
                Id = card.Id,
                CardNo = card.CardNo,
                UserId = card.UserId,
                UserName = card.UserName,
                Phone = card.Phone,
                ProductName = card.ProductName,
                ProductBrand = card.ProductBrand,
                ProductType = card.ProductType,
                ProductModel = card.ProductModel,
                PurchaseDate = card.PurchaseDate,
                WarrantyYears = card.WarrantyYears,
                PaidAmount = card.PaidAmount,
                StartDate = card.StartDate,
                EndDate = card.EndDate,
                CardStatus = card.CardStatus,
                CardStatusName = GetCardStatusName(card.CardStatus),
                IsExpired = isExpired,
                RemainingDays = remainingDays,
                OrderId = card.OrderId,
                ParentCardId = card.ParentCardId,
                IsRenewal = card.IsRenewal,
                CreateTime = card.CreateTime,
                UpdateTime = card.UpdateTime
            };
        }

        #endregion
    }
}