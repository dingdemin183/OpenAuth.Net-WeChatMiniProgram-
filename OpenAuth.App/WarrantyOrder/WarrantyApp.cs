using Castle.Core.Logging;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using OpenAuth.App.AuditWarrantyOrder;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.WxPay;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Enums;
using SqlSugar;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAuth.App.Warranty
{
    public class WarrantyApp : SqlSugarBaseApp<WarrantyRecord>
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuth _auth;
        private readonly WxPayService _wxPayService;
        private readonly WxPayRefundService _wxPayRefundService;
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly ILogger<WarrantyApp> _logger;

        public WarrantyApp(
            ISqlSugarClient db,
            IAuth auth,
            WxPayService wxPayService,
            WxPayRefundService wxPayRefundService,
            IOptions<AppSetting> appConfiguration,
            ILogger<WarrantyApp> logger) : base(db, auth)
        {
            _db = db;
            _auth = auth;
            _wxPayService = wxPayService;
            _wxPayRefundService = wxPayRefundService;
            _appConfiguration = appConfiguration;
            _logger = logger;

        }

        // OpenAuth.App/Warranty/WarrantyApp.cs

       

        #region 订单相关

        /// <summary>
        /// 创建延保支付订单（支持首次支付和二次支付）
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task<WeChatPayResp> CreatePayOrderAsync(CreateWarrantyPayOrderReq req)
        {
            // 参数校验
            if (req == null)
                throw new CommonException("请求参数不能为空");
            if (req.Amount <= 0)
                throw new CommonException("支付金额必须大于0");
            if (req.WarrantyYears <= 0)
                throw new CommonException("延保年限必须大于0");

            // 获取当前用户
            var user = _auth.GetCurrentSession();
            if (string.IsNullOrEmpty(user?.UserId))
                throw new CommonException("登录信息错误，请重新登录");

            // 获取用户openid
            var externalAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == user.UserId)
                .ConfigureAwait(false);

            if (externalAuth == null || string.IsNullOrEmpty(externalAuth.OpenId))
                throw new CommonException("未获取到微信登录信息，请重新登录");

            // 校验购机时间（购机1年内才能购买）
            var now = DateTime.Now;
            var oneYearLater = req.PurchaseDate.AddYears(1);
            if (now > oneYearLater)
                throw new CommonException("购机一年内才可以买延保卡，超过一年不可以购买");

            WarrantyRecord record;

            if (string.IsNullOrEmpty(req.OrderNo))
            {
                // 首次支付：创建新订单
                record = await CreateNewOrderAsync(req, user.UserId);
            }
            else
            {
                // 二次支付：更新已有订单 
                record = await UpdateOrderForRepayAsync(req, user.UserId);
            }
            try
            {
                var payResult = await _wxPayService.UnifiedOrderAsync(req,user.Account,record.OrderNo);

                record.UpdateTime = now;

                await _db.Updateable(record)
                    .UpdateColumns(x => new { x.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                return payResult;
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"创建支付订单失败：订单{record.OrderNo}");
                throw new CommonException("创建支付订单失败，请重试");
            }

           
        }

        /// <summary>
        /// 首次支付：创建新订单
        /// </summary>
        /// <param name="req"></param>
        /// <param name="userId"></param>
        /// <param name="deviceUniqueKey"></param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        private async Task<WarrantyRecord> CreateNewOrderAsync(CreateWarrantyPayOrderReq req, string userId)
        {
            // 生成订单号
            var orderNo = await GenerateOrderNoAsync();

            var record = new WarrantyRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                OrderNo = orderNo,
                UserId = userId,
                UserName = req.UserName,
                Phone = req.Phone,
                ProductBrand = req.ProductBrand,
                ProductType = req.ProductType,
                ProductModel = req.ProductModel,
                PurchaseDate = req.PurchaseDate,
                EnergyImage = req.EnergyImage,
                TradeImage = req.TradeImage,
                WarrantyYears = req.WarrantyYears,
                Amount = req.Amount,
                OrderStatus = 0,
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            await _db.Insertable(record).ExecuteCommandAsync().ConfigureAwait(false);
            return record;

        }

        /// <summary>
        /// 二次支付：更新已有订单
        /// </summary>
        /// <param name="req">请求参数</param>
        /// <param name="userId">用户编号</param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        private async Task<WarrantyRecord> UpdateOrderForRepayAsync(CreateWarrantyPayOrderReq req, string userId)
        {
            // 查询订单
            var record = await _db.Queryable<WarrantyRecord>()
                .Where(r => r.OrderNo == req.OrderNo && r.UserId == userId && !r.IsDeleted)
                .FirstAsync()
                .ConfigureAwait(false);

            if (record == null)
                throw new CommonException("订单不存在，请检查订单号");

            // 只有"待支付"状态才能重新支付
            if (record.OrderStatus != 0)
            {
                throw new CommonException($"当前订单状态非“待支付”，无法重新支付");
            }

            // 更新订单信息
            record.UserName = req.UserName;
            record.Phone = req.Phone;
            record.ProductBrand = req.ProductBrand;
            record.ProductType = req.ProductType;
            record.ProductModel = req.ProductModel;
            record.PurchaseDate = req.PurchaseDate;
            record.EnergyImage = req.EnergyImage;
            record.TradeImage = req.TradeImage;
            record.WarrantyYears = req.WarrantyYears;
            record.Amount = req.Amount;
            record.OrderStatus = 0;  // 待支付状态
            record.UpdateTime = DateTime.Now;

            await _db.Updateable(record)
                .UpdateColumns(r => new
                {
                    r.UserName,
                    r.Phone,
                    r.ProductBrand,
                    r.ProductType,
                    r.ProductModel,
                    r.PurchaseDate,
                    r.EnergyImage,
                    r.TradeImage,
                    r.WarrantyYears,
                    r.Amount,
                    r.OrderStatus,
                    r.UpdateTime
                })
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            return record;

        }
        /// <summary>
        /// 延保订单审核（通过/拒绝，拒绝时自动发起退款）
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public async Task<Response<bool>> AuditWarrantyOrderAsync(AuditWarrantyOrderReq req)
        {
            var result = new Response<bool>();

            // 参数校验
            if (string.IsNullOrWhiteSpace(req.OrderNo))
            {
                throw new CommonException("延保订单号不能为空");
            }

            if (!req.IsApproved && string.IsNullOrWhiteSpace(req.Remark))
            {
                throw new CommonException("审核拒绝时必填拒绝原因");
            }

            // 查询订单
            var order = await _db.Queryable<WarrantyRecord>()
                .Where(o => o.OrderNo == req.OrderNo && !o.IsDeleted)
                .FirstAsync()
                .ConfigureAwait(false);

            if (order == null)
            {
                throw new CommonException("延保订单不存在");
            }

            // 检查订单状态
            if (order.OrderStatus != WarrantyStatusEnum.Paid && order.OrderStatus != WarrantyStatusEnum.RefundFailed)
            {
                throw new CommonException("订单未支付或已处理，无法审核");
            }

            // 执行审核
            if (req.IsApproved)
            {
                // 审核通过
                if (req.EndTime == null || req.EndTime <= DateTime.Now)
                {
                    throw new CommonException("通过时必填延保有效期，延保到期时间必须大于当前时间");
                }

                order.OrderStatus = WarrantyStatusEnum.Active;
                order.StartDate = DateTime.Now;
                order.EndDate = req.EndTime;
                order.UpdateTime = DateTime.Now;

                await _db.Updateable(order)
                    .UpdateColumns(o => new { o.OrderStatus, o.StartDate, o.EndDate, o.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                result.Code = 200;
                result.Message = "审核通过";
                result.Data = true;
            }
            else
            {
                // 审核拒绝 - 使用新版 V3 退款
                // 先更新状态为"已退款"
                order.OrderStatus = WarrantyStatusEnum.Refunded;
                order.AuditRemark = req.Remark;
                order.UpdateTime = DateTime.Now;

                await _db.Updateable(order)
                    .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                try
                {
                    // 生成商户退款单号
                    var refundNo = _wxPayRefundService.GenerateRefundNo(order.OrderNo);

                    var config = _appConfiguration.Value.WeChatPay;

                    var refundReq = new RefundReq
                    {
                        TransactionId = order.TransactionId,  // 微信支付订单号
                        OutRefundNo = refundNo,
                        Reason = "延保订单审核不通过，退款",
                        Amount = new RefundAmount
                        {
                            Total = (int)(order.Amount * 100),    // 原订单金额（分）
                            Refund = (int)(order.Amount * 100),   // 退款金额（分）
                            Currency = "CNY"
                        }
                    };
                    //调用新版 V3 退款接口
                    var refundResult = await _wxPayRefundService.CreateRefundAsync(refundReq,config);

                    // 退款已发起，等待微信回调
                    _logger.LogInformation($"退款已发起：订单{order.OrderNo}，退款单号{refundNo}，等待回调");

                    // 更新退款单号到订单
                    order.RefundNo = refundNo;
                    order.OrderStatus = WarrantyStatusEnum.Refunded;  // 退款中状态
                    order.UpdateTime = DateTime.Now;

                    await _db.Updateable(order)
                        .UpdateColumns(o => new { o.RefundNo, o.OrderStatus, o.UpdateTime })
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    result.Code = 200;
                    result.Message = "审核拒绝，退款已发起";
                    result.Data = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"发起退款失败：订单{order.OrderNo}");

                    // 退款失败，更新状态
                    order.OrderStatus = WarrantyStatusEnum.RefundFailed;
                    order.AuditRemark = $"退款失败：{ex.Message}";
                    order.UpdateTime = DateTime.Now;

                    await _db.Updateable(order)
                        .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    throw new CommonException($"退款发起失败：{ex.Message}");
                }
            }

            return result;
        }



        /// <summary>
        /// 查询订单支付状态（小程序端轮询）
        /// </summary>
        /// <param name="orderNo"></param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task<WarrantyCardResp> QueryOrderStatusAsync(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                throw new CommonException("订单号不能为空");

            try
            {
                var order = await _db.Queryable<WarrantyRecord>()
               .Where(o => o.OrderNo == orderNo && !o.IsDeleted)
               .FirstAsync()
               .ConfigureAwait(false);

                if (order == null)
                    throw new CommonException("订单不存在");

                return new WarrantyCardResp
                {
                    Id = order.Id,
                    OrderNo = order.OrderNo,
                    CardStatus = (int)order.OrderStatus,
                    CardStatusName = EnumExtensions.GetText(order.OrderStatus),
                    TransactionId = order.TransactionId,
                    PaidAmount = order.Amount
                };
            }
            catch (Exception ex)
            {
                throw new CommonException("查询订单状态失败");
            }   


        }

        #endregion

        #region 延保卡相关


        /// <summary>
        /// 获取延保卡详情
        /// </summary>
        /// <param name="orderNo">延保订单号</param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task<WarrantyCardResp> GetCardDetailAsync(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                throw new CommonException("延保卡订单号不能为空");

            try
            {
                var card = await _db.Queryable<WarrantyRecord>()
               .Where(c => c.OrderNo == orderNo && !c.IsDeleted)
               .FirstAsync()
               .ConfigureAwait(false);

                if (card == null)
                    throw new CommonException("延保卡不存在");

                return MapToCardResp(card);

            }
            catch (Exception ex)
            {
                throw new CommonException("获取延保卡信息失败");
            }
           
        }

        /// <summary>
        /// 获取用户的延保卡列表（小程序端）
        /// </summary>
        public async Task<List<WarrantyCardResp>> GetUserCardsAsync(string userId)
        {

            //if (string.IsNullOrWhiteSpace(userId))
            //    throw new CommonException("用户未登录");
            try
            {
                var cards = await _db.Queryable<WarrantyRecord>()
                                      .Where(r => r.UserId == userId && !r.IsDeleted)
                                      .ToListAsync()
                                      .ConfigureAwait(false);

                // 内存排序
                return cards
                    .OrderBy(r => r.OrderStatus == WarrantyStatusEnum.Active ? 0 : 1)
                    .ThenByDescending(r => r.CreateTime)
                    .Select(c => MapToCardResp(c))
                    .ToList();

            }
            catch (Exception ex)
            {
                throw new CommonException("获取用户延保卡列表失败");
            }

        }



        #endregion

        #region 编号生成

        /// <summary>
        /// 生成订单号
        /// </summary>
        private async Task<string> GenerateOrderNoAsync()
        {
            var maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                var orderNo = GenerateOrderNoInternal();

                // 检查订单号是否已存在
                var exists = await _db.Queryable<WarrantyRecord>()
                    .AnyAsync(x => x.OrderNo == orderNo);

                if (!exists)
                {
                    return orderNo;
                }
                await Task.Delay(1);
            }

            // 重试失败，使用带时间戳更精确的版本
            return GenerateOrderNoWithTicks();
        }

        /// <summary>
        /// 生成订单号：WB + 日期(8位) + 6位随机数
        /// </summary>
        private string GenerateOrderNoInternal()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(100000, 999999).ToString();
            return $"WB{datePart}{random}";
        }

        /// <summary>
        /// 生成订单号（带毫秒级时间戳，用于重试失败时）
        /// </summary>
        private string GenerateOrderNoWithTicks()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var ticks = DateTime.Now.Ticks.ToString().Substring(12, 4); // 取后4位
            var random = new Random().Next(10, 99).ToString();
            return $"WB{datePart}{ticks}{random}";
        }

        #endregion


        #region 私有方法

        /// <summary>
        /// 映射延保卡实体到响应
        /// </summary>
        /// <param name="card">延保卡信息</param>
        /// <returns></returns>
        private WarrantyCardResp MapToCardResp(WarrantyRecord card)
        {
            var now = DateTime.Now;
            var isExpired = card.EndDate < now;

            //  动态计算状态名称
            var displayStatus = card.OrderStatus;
            var displayStatusName = EnumExtensions.GetText(card.OrderStatus);

            // 如果数据库状态是"生效中"，但实际已过期，显示为"已过期"
            if (isExpired && card.OrderStatus == WarrantyStatusEnum.Active)
            {
                displayStatus = WarrantyStatusEnum.Expired;
                displayStatusName = "已过期";
            }
            //如果数据状态是退款失败，显示为已支付
            if(card.OrderStatus == WarrantyStatusEnum.RefundFailed)
            {
                displayStatus = WarrantyStatusEnum.Paid;
                displayStatusName = "已支付";
            }

            return new WarrantyCardResp
            {
                Id = card.Id,
                UserId = card.UserId,
                UserName = card.UserName,
                Phone = card.Phone,
                ProductBrand = card.ProductBrand,
                ProductType = card.ProductType,
                ProductModel = card.ProductModel,
                PurchaseDate = card.PurchaseDate,
                WarrantyYears = card.WarrantyYears,
                PaidAmount = card.Amount,
                PayTime = card.PayTime,
                EndDate = card.EndDate,
                RemainingDays = card.EndDate.HasValue ? (card.EndDate.Value - now).Days : 0,
                CardStatus =(int) displayStatus,          // 返回修正后的状态
                CardStatusName = displayStatusName,  // 返回修正后的状态名
                OrderNo = card.OrderNo,
            };
        }

        
        #endregion
    }
}