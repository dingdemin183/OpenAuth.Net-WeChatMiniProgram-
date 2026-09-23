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

        /// <summary>
        /// 后台查询延保卡列表（支持分页、模糊查询、状态筛选、时间范围）
        /// </summary>
        public async Task<TableResp<WarrantyCardResp>> QueryWarrantyCardsAsync(QueryWarrantyCardsReq req)
        {
            if (req == null)
                throw new CommonException("请求参数不能为空");

            var query = _db.Queryable<WarrantyRecord>()
                .Where(r => r.IsDeleted == false);

            // 订单状态筛选
            if (req.OrderStatus.HasValue)
            {
                var statusValue = req.OrderStatus.Value;
                if (!Enum.IsDefined(typeof(WarrantyStatusEnum), statusValue))
                    throw new CommonException("非法的订单状态值");

                var statusEnum = (WarrantyStatusEnum)statusValue;
                query = query.Where(r => r.OrderStatus == statusEnum);
            }

            // 关键词查询：字段值包含用户输入的关键词（修复原来方向写反的问题）
            if (!string.IsNullOrWhiteSpace(req.Key))
            {
                var key = req.Key.Trim();
                query = query.Where(r => r.UserId.Contains(key)
                                         || r.UserName.Contains(key)
                                         || r.Phone.Contains(key));
            }
            if (!string.IsNullOrWhiteSpace(req.OrderNo))
            {
                query = query.Where(r => r.OrderNo.Contains(req.OrderNo.Trim()));
            }

            // 创建时间范围筛选
            if (req.StartTime.HasValue)
            {
                var start = req.StartTime.Value.Date;
                query = query.Where(r => r.CreateTime >= start);
            }
            if (req.EndTime.HasValue)
            {
                var end = req.EndTime.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(r => r.CreateTime <= end);
            }
            // 简单修复：忽略 sort 参数，使用固定排序
            query = query.OrderByDescending(r => r.CreateTime);

            // 分页查询
            var total = await query.CountAsync().ConfigureAwait(false);
            var list = await query.ToPageListAsync(req.Page, req.Limit).ConfigureAwait(false);

            // 映射为响应对象
            var data = list.Select(c => MapToCardResp(c)).ToList();

            return new TableResp<WarrantyCardResp>
            {
                Data = data,
                Count = total,
                Page = req.Page,
                Limit = req.Limit
            };
        }



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
            if(string.IsNullOrEmpty(user?.Account))
            {
                throw new CommonException("未获取到微信登录信息，请重新登录");
            }
            //var externalAuth = await _db.Queryable<SysUserExternalAuth>()
            //    .FirstAsync(x => x.Id == user.UserId)
            //    .ConfigureAwait(false);

            //if (externalAuth == null || string.IsNullOrEmpty(externalAuth.OpenId))
            //    throw new CommonException("未获取到微信登录信息，请重新登录");

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

            // 微信下单属于外部HTTP调用，不能包进数据库事务
            var payResult = await _wxPayService.UnifiedOrderAsync(req, user.Account, record.OrderNo);

            record.UpdateTime = DateTime.Now;

            // 只把数据库写操作包在事务里
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Updateable(record)
                    .UpdateColumns(x => new { x.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }

            return payResult;
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
            try
            {
                // 生成订单号
                var orderNo = GenerateOrderNoInternal();

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

                // 插入写操作包在事务里
                await _db.Ado.BeginTranAsync();
                try
                {
                    await _db.Insertable(record)
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    await _db.Ado.CommitTranAsync();
                }
                catch
                {
                    await _db.Ado.RollbackTranAsync();
                    throw;
                }
                return record;
            }
            catch (CommonException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建延保订单失败");
                throw new CommonException("创建延保订单失败,请重试");
            }
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
                .Where(r => r.OrderNo == req.OrderNo && r.UserId == userId && r.IsDeleted == false)
                .FirstAsync()
                .ConfigureAwait(false);

            if (record == null)
                throw new CommonException("订单不存在，请检查订单号");

            // 只有"待支付"状态才能重新支付
            if (record.OrderStatus != 0)
            {
                throw new CommonException($"当前订单状态非“待支付”，无法重新支付");
            }

            // 更新订单信息（查询已在事务外完成，这里只做写操作）
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
            record.OrderStatus = WarrantyStatusEnum.Pending;  // 待支付状态
            record.UpdateTime = DateTime.Now;

            await _db.Ado.BeginTranAsync();
            try
            {
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

                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }

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
                .Where(o => o.OrderNo == req.OrderNo && o.IsDeleted == false)
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

                // 查询在事务外，只把审核写操作包在事务里
                await _db.Ado.BeginTranAsync();
                try
                {
                    await _db.Updateable(order)
                        .UpdateColumns(o => new { o.OrderStatus, o.StartDate, o.EndDate, o.UpdateTime })
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    await _db.Ado.CommitTranAsync();
                }
                catch
                {
                    await _db.Ado.RollbackTranAsync();
                    throw;
                }

                result.Code = 200;
                result.Message = "审核通过";
                result.Data = true;
            }
            else
            {
                // 审核拒绝 - 使用新版 V3 退款
                // 先更新状态为"已退款"并记录拒绝原因（写操作包事务）
                order.OrderStatus = WarrantyStatusEnum.Refunded;
                order.AuditRemark = req.Remark;
                order.UpdateTime = DateTime.Now;

                await _db.Ado.BeginTranAsync();
                try
                {
                    await _db.Updateable(order)
                        .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    await _db.Ado.CommitTranAsync();
                }
                catch
                {
                    await _db.Ado.RollbackTranAsync();
                    throw;
                }

                try
                {
                    // 生成商户退款单号
                    var refundNo = _wxPayRefundService.GenerateRefundNo(order.OrderNo);

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

                    // 调用新版 V3 退款接口（外部HTTP调用，不能包进数据库事务）
                    await _wxPayRefundService.CreateRefundAsync(refundReq);

                    // 退款已发起，等待微信回调
                    _logger.LogInformation("退款已发起：订单{OrderNo}，退款单号{RefundNo}，等待回调",
                        order.OrderNo, refundNo);

                    // 更新退款单号到订单（写操作包事务）
                    order.RefundNo = refundNo;
                    order.OrderStatus = WarrantyStatusEnum.Refunded;  // 退款中状态，等待微信回调确认
                    order.UpdateTime = DateTime.Now;

                    await _db.Ado.BeginTranAsync();
                    try
                    {
                        await _db.Updateable(order)
                            .UpdateColumns(o => new { o.RefundNo, o.OrderStatus, o.UpdateTime })
                            .ExecuteCommandAsync()
                            .ConfigureAwait(false);

                        await _db.Ado.CommitTranAsync();
                    }
                    catch
                    {
                        await _db.Ado.RollbackTranAsync();
                        throw;
                    }

                    result.Code = 200;
                    result.Message = "审核拒绝，退款已发起";
                    result.Data = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "发起退款失败：订单{OrderNo}", order.OrderNo);

                    // 退款失败，回写失败状态；若回写也失败，只记录日志，避免掩盖原始退款异常
                    try
                    {
                        order.OrderStatus = WarrantyStatusEnum.RefundFailed;
                        order.AuditRemark = $"退款失败：{ex.Message}";
                        order.UpdateTime = DateTime.Now;

                        await _db.Ado.BeginTranAsync();
                        try
                        {
                            await _db.Updateable(order)
                                .UpdateColumns(o => new { o.OrderStatus, o.AuditRemark, o.UpdateTime })
                                .ExecuteCommandAsync()
                                .ConfigureAwait(false);

                            await _db.Ado.CommitTranAsync();
                        }
                        catch
                        {
                            await _db.Ado.RollbackTranAsync();
                            throw;
                        }
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "退款失败后回写订单状态也失败：订单{OrderNo}", order.OrderNo);
                    }

                    throw new CommonException($"退款发起失败：{ex.Message}");
                }
            }

            return result;
        }

        // OpenAuth.App/Warranty/WarrantyApp.cs 中添加

        /// <summary>
        /// 查询订单状态（先查本地，若本地为待支付则主动查询微信并同步）
        /// </summary>
        /// <param name="orderNo">商户订单号</param>
        /// <returns></returns>
        public async Task<WarrantyOrderQueryResp> QueryOrderStatusFromWechatAsync(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                throw new CommonException("订单号不能为空");

            // 查本地订单
            var order = await _db.Queryable<WarrantyRecord>()
                .Where(o => o.OrderNo == orderNo && o.IsDeleted == false)
                .FirstAsync()
                .ConfigureAwait(false);

            if (order == null)
                throw new CommonException("订单不存在");

            // 如果本地已经是终态（已支付待审核/生效中/已过期/已退款/退款失败），直接返回本地数据
            if (order.OrderStatus != WarrantyStatusEnum.Pending)
            {
                return BuildQueryResp(order, null);
            }

            // 本地为"待支付"，主动去微信查询
            try
            {
                var wxResp = await _wxPayService.QueryTransactionByOutTradeNoAsync(orderNo);

                // 微信侧订单不存在，直接返回本地状态
                if (wxResp == null)
                {
                    return BuildQueryResp(order, null);
                }

                // 据微信返回的交易状态同步本地订单
                bool needUpdate = false;

                switch (wxResp.TradeState)
                {
                    case "SUCCESS":
                        // 支付成功，但回调可能丢失，这里主动同步
                        order.OrderStatus = WarrantyStatusEnum.Paid;
                        order.TransactionId = wxResp.TransactionId;

                        // 
                        if (wxResp.SuccessTime.HasValue)
                        {
                            order.PayTime = wxResp.SuccessTime.Value.LocalDateTime;
                        }
                        else
                        {
                            order.PayTime = DateTime.Now;
                        }
                        needUpdate = true;
                        _logger.LogInformation("主动同步微信支付成功：订单{OrderNo}", orderNo);
                        break;

                    case "REFUND":
                        order.OrderStatus = WarrantyStatusEnum.Refunded;
                        needUpdate = true;
                        break;

                    case "CLOSED":
                        // 订单已关闭，可视为失效（这里按已过期处理，或你自定义状态）
                        order.OrderStatus = WarrantyStatusEnum.Expired;
                        needUpdate = true;
                        break;

                    case "NOTPAY":
                    case "USERPAYING":
                        // 未支付 / 支付中，保持待支付状态
                        break;

                    case "PAYERROR":
                    case "REVOKED":
                        // 支付失败/已撤销，保持待支付，让用户重新支付
                        break;

                    default:
                        _logger.LogWarning("未知微信交易状态：{TradeState}，订单{OrderNo}",
                            wxResp.TradeState, orderNo);
                        break;
                }

                if (needUpdate)
                {
                    order.UpdateTime = DateTime.Now;
                    // 状态同步写操作包事务
                    await _db.Ado.BeginTranAsync();
                    try
                    {
                        await _db.Updateable(order)
                            .UpdateColumns(o => new
                            {
                                o.OrderStatus,
                                o.TransactionId,
                                o.PayTime,
                                o.UpdateTime
                            })
                            .ExecuteCommandAsync()
                            .ConfigureAwait(false);

                        await _db.Ado.CommitTranAsync();
                    }
                    catch
                    {
                        await _db.Ado.RollbackTranAsync();
                        throw;
                    }
                }

                return BuildQueryResp(order, wxResp);
            }
            catch (Exception ex)
            {
                // 微信查询失败不影响本地状态返回
                _logger.LogError(ex, "查询微信订单异常：订单{OrderNo}", orderNo);
                return BuildQueryResp(order, null);
            }
        }

        /// <summary>
        /// 构建订单查询响应
        /// </summary>
        private WarrantyOrderQueryResp BuildQueryResp(
            WarrantyRecord order,
            SKIT.FlurlHttpClient.Wechat.TenpayV3.Models.GetPayTransactionByOutTradeNumberResponse wxResp)
        {
            var now = DateTime.Now;

            // 计算显示状态（与 MapToCardResp 保持一致）
            var displayStatus = order.OrderStatus;
            var displayStatusName = EnumExtensions.GetText(order.OrderStatus);

            if (order.EndDate.HasValue && order.EndDate.Value < now &&
                order.OrderStatus == WarrantyStatusEnum.Active)
            {
                displayStatus = WarrantyStatusEnum.Expired;
                displayStatusName = "已过期";
            }

            if (order.OrderStatus == WarrantyStatusEnum.RefundFailed)
            {
                displayStatus = WarrantyStatusEnum.Paid;
                displayStatusName = "已支付";
            }

            return new WarrantyOrderQueryResp
            {
                Id = order.Id,
                OrderNo = order.OrderNo,
                TransactionId = order.TransactionId,
                CardStatus = (int)displayStatus,
                CardStatusName = displayStatusName,
                TradeState = wxResp?.TradeState,
                TradeStateDesc = wxResp?.TradeStateDescription,
                PaidAmount = order.Amount,
                PayTime = order.PayTime
            };
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

            var order = await _db.Queryable<WarrantyRecord>()
                .Where(o => o.OrderNo == orderNo && o.IsDeleted == false)
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
            var card = await _db.Queryable<WarrantyRecord>()
              .Where(c => c.OrderNo == orderNo && c.IsDeleted == false)
              .FirstAsync()
              .ConfigureAwait(false);

            if (card == null)
                throw new CommonException("延保卡不存在");

            return MapToCardResp(card);
    
           
        }

        /// <summary>
        /// 获取用户的延保卡列表（小程序端）
        /// </summary>
        public async Task<List<WarrantyCardResp>> GetUserCardsAsync(string userId)
        {

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new CommonException("用户未登录");
            }

            var cards = await _db.Queryable<WarrantyRecord>()
                                     .Where(r => r.UserId == userId && r.IsDeleted == false)
                                     .ToListAsync()
                                     .ConfigureAwait(false);

            // 内存排序
            return cards
                .OrderBy(r => r.OrderStatus == WarrantyStatusEnum.Active ? 0 : 1)
                .ThenByDescending(r => r.CreateTime)
                .Select(c => MapToCardResp(c))
                .ToList();
          

        }



        #endregion

        #region 编号生成

      

        /// <summary>
        /// 生成订单号
        /// </summary>
        /// <returns></returns>
        private string GenerateOrderNoInternal()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            return $"DSYB{datePart}{suffix}"; 
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
            var isExpired = card.EndDate.HasValue && card.EndDate.Value < now;

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
            string remainingdays = "";
            if (displayStatus == WarrantyStatusEnum.Expired)
            {
                remainingdays = "-";
            }
            else
            {
                remainingdays = card.EndDate.HasValue
                    ? ((int)(card.EndDate.Value - now).TotalDays).ToString()
                    : "";
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
                RemainingDays = remainingdays,
                CardStatus =(int) displayStatus,          // 返回修正后的状态
                CardStatusName = displayStatusName,  // 返回修正后的状态名
                OrderNo = card.OrderNo,
            };
        }

        
        #endregion
    }
}