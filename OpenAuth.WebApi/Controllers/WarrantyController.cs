using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.AuditWarrantyOrder;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.Warranty;
using OpenAuth.App.WxPay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 延保管理
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "延保管理_Warranty")]
    public class WarrantyController : ControllerBase
    {
        private readonly WarrantyApp _warrantyApp;
        private readonly IAuth _auth;
        private readonly WxPayService _wxPayService;

        public WarrantyController(WarrantyApp warrantyApp,IAuth auth,WxPayService wxPayService)
        {
            _warrantyApp = warrantyApp;
            _wxPayService = wxPayService;
            _auth = auth;
        }

        /// <summary>
        /// 测试微信支付V3签名（使用官方示例数据）
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous]
        public Response<object> TestSign()
        {
            var result = new Response<object>();
            try
            {
                // 调用测试方法
                var testResult = _wxPayService.TestSign();

                result.Code = 200;
                result.Message = "测试完成";
                result.Data = testResult;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = $"测试失败：{ex.Message}";
            }
            return result;
        }
        //测试签名工具类
        [HttpGet]
        [AllowAnonymous]
        public IActionResult TestSignWithSigner()
        {
            try
            {
                // 使用配置中的证书路径（或直接使用测试路径）
                var privateKeyPath = @"C:\NewOpenAuth\OpenAuth.Net-WeChatMiniProgram-\OpenAuth.WebApi\Certificates\apiclient_key.pem";

                var mchId = "1900007291";
                var serialNo = "408B07E79B8269FEC3D5D3E6AB8ED163A6A380DB";

                var signer = new WeChatPayV3Signer(privateKeyPath, mchId, serialNo);

                // 官方测试数据
                var method = "POST";
                var url = "/v3/pay/transactions/jsapi";
                var timestamp = "1554208460";
                var nonceStr = "593BEC0C930BF1AFEB40B4A08C8FB242";
                var body = "{\"appid\":\"wxd678efh567hg6787\",\"mchid\":\"1900007291\",\"description\":\"Image形象店-深圳腾大-QQ公仔\",\"out_trade_no\":\"1217752501201407033233368018\",\"notify_url\":\"https://www.weixin.qq.com/wxpay/pay.php\",\"amount\":{\"total\":100,\"currency\":\"CNY\"},\"payer\":{\"openid\":\"oUpF8uMuAJO_M2pxb1Q9zNjWeS6o\"}}";

                var expectedSignature = "jnks4dlrPw3ZX+ozVvSK39oa0t7OMBsg83BHAwd8BRdUFiVaQNTLTvci+wURgP1OQBbKYhFGvt7iqYpDSTQkp7Uq1sltaQKyncCyrA1g88m5bsKERQfPyT0ahSwKTYJ1CAn9QiJuSJRq1QsQs07eehbU/k9BCS51jTyc1Jpsi2H77HF9f/BnjXAOP3/sPObg6V5Ee4EzwLox684hhuMuIwHo7D8KFk3LIHOKDcNI4It1aCXydFWNpNK+SG86VUDe5kwoDpw4Ulqfu9z8OFDGbDs9TCxEv8iqQzbpxOlEVoOe2kalSYM5kApQb3nZcxdUtoE0liJGW3RGUNE0t4v01A==";

                // 使用 signer 生成签名
                var actualSignature = signer.Sign($"{method}\n{url}\n{timestamp}\n{nonceStr}\n{body}\n");
                var authorization = signer.GenerateAuthorization(method, url, body, nonceStr, timestamp);

                return Ok(new
                {
                    ExpectedSignature = expectedSignature,
                    ActualSignature = actualSignature,
                    IsMatch = expectedSignature == actualSignature,
                    Authorization = authorization,
                    Note = expectedSignature == actualSignature ? "签名工具类正确！" : "签名不匹配"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }



        /// <summary>
        /// 创建延保新订单并返回支付参数
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<WeChatPayResp>> CreatePayOrder([FromBody] CreateWarrantyPayOrderReq request)
        {
            var result = new Response<WeChatPayResp>();
            try
            {
                var payResult = await _warrantyApp.CreatePayOrderAsync(request);
                result.Code = 200;
                result.Message = "订单创建成功";
                result.Data = payResult;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 延保订单审核（通过/拒绝，拒绝时自动退款）
        /// </summary>
        [HttpPost]
        public async Task<Response<bool>> AuditWarrantyOrder([FromBody] AuditWarrantyOrderReq request)
        {
            var result = new Response<bool>();
            try
            {
                result = await _warrantyApp.AuditWarrantyOrderAsync(request);
                result.Code = 200;
                result.Message = "订单创建成功";
                result.Data = result.Data;

            }
            catch (CommonException ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }


        /// <summary>
        /// 查询订单支付状态
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<WarrantyCardResp>> QueryOrderStatus(string orderNo)
        {
            var result = new Response<WarrantyCardResp>();
            try
            {
                var data = await _warrantyApp.QueryOrderStatusAsync(orderNo);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取当前用户的延保卡列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<List<WarrantyCardResp>>> MyCards()
        {
            var result = new Response<List<WarrantyCardResp>>();
            try
            {
                // 从当前登录上下文获取用户ID
                //var userId = GetWxUserId();
                string userId = "test_user_001";
                var data = await _warrantyApp.GetUserCardsAsync(userId);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取延保卡详情
        /// </summary>
        /// <param name="orderNo">延保订单号</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<WarrantyCardResp>> CardDetail(string orderNo)
        {
            var result = new Response<WarrantyCardResp>();
            try
            {
                var data = await _warrantyApp.GetCardDetailAsync(orderNo);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取当前登录用户的ID
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetWxUserId()
        {
            var session = _auth.GetCurrentSession();
            if (session?.UserId == null)
            {
                throw new Exception("用户未登录");
            }
            return session.UserId;
        }
    }
}