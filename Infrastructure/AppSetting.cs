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


    /// <summary>
    /// 微信支付配置(V3版本）
    /// </summary>
    public class WeChatPaySetting
    {
        /// <summary>
        /// 商户号
        /// </summary>
        public string MchId { get; set; }

        /// <summary>
        /// 小程序 AppId
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 商户证书序列号（从 apiclient_cert.pem 中读取）
        /// </summary>
        public string SerialNo { get; set; }

        /// <summary>
        /// 商户私钥文件路径（apiclient_key.pem，PKCS#8 格式）
        /// </summary>
        public string PrivateKeyPath { get; set; }

        /// <summary>
        /// 商户证书文件路径（apiclient_cert.p12，用于退款等需要双向证书的场景）
        /// </summary>
        public string CertPath { get; set; }

        /// <summary>
        /// 微信支付平台证书路径
        /// </summary>
        public string PlatformCertPath { get; set; }

        /// <summary>
        /// API V3 密钥（用于回调解密）
        /// </summary>
        public string ApiV3Key { get; set; }

        /// <summary>
        /// 微信支付回调通知地址
        /// </summary>
        public string NotifyUrl { get; set; }


        /// <summary>
        /// 退款回调通知地址
        /// </summary>
        public string RefundNotifyUrl { get; set; }
    }
}
