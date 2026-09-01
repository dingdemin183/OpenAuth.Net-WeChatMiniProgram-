using System.Collections.Generic;

namespace Infrastructure
{
    /// <summary>
    /// 配置项
    /// </summary>
    public class AppSetting
    {

        public AppSetting()
        {
            SSOPassport = "http://localhost:52789";
            Version = "";
            UploadPath = "";
            IdentityServerUrl = "";
        }

        /// <summary>
        /// 微信支付配置
        /// </summary>
        public WeChatPaySetting WeChatPay { get; set; }

        /// <summary>
        /// 小程序登录配置
        /// </summary>
        public WeChatMiniProgramConfig WeChatMiniProgram { get; set; }
        /// <summary>
        /// SSO地址
        /// </summary>
        public string SSOPassport { get; set; }

        /// <summary>
        /// 版本信息
        /// 如果为demo,则屏蔽Post请求
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 数据库类型 SqlServer、MySql
        /// </summary>
        public Dictionary<string, string> DbTypes { get; set; }

        /// <summary> 附件上传路径</summary>
        public string UploadPath { get; set; }

        //identity授权的地址
        public string IdentityServerUrl { get; set; }

        /// <summary>
        /// Redis服务器配置
        /// </summary>
        public string RedisConf { get; set; }

        /// <summary>
        /// JWT签名密钥，用于本地认证模式下生成和验证JWT Token
        /// </summary>
        public string JwtSecret { get; set; } = "openauth_default_jwt_secret_key_2024";

        /// <summary>
        /// JWT Token过期天数，默认10天
        /// </summary>
        public int JwtExpireDays { get; set; } = 10;

        //是否是Identity授权方式
        public bool IsIdentityAuth => !string.IsNullOrEmpty(IdentityServerUrl);
    }
    public class DbTypesConfig
    {
        public string OpenAuthDBContext { get; set; }
        public string OpenAuthDBContext2 { get; set; }
        public string OpenAuthDBContext3 { get; set; }
    }
    public class WeChatMiniProgramConfig
    {
        public string AppId { get; set; }
        public string AppSecret { get; set; }
    }

    public class WeChatPaySetting
    {
        /// <summary>
        /// 小程序AppId
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 商户号
        /// </summary>
        public string MchId { get; set; }

        /// <summary>
        /// API密钥
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// 支付回调地址
        /// </summary>
        public string NotifyUrl { get; set; }

    }
}
