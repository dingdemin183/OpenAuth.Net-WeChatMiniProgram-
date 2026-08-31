using System;
using System.Linq;
using Autofac;
using Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAuth.App;
using OpenAuth.Repository;
using OpenIddict.Abstractions;
using SqlSugar;
using OpenIddict.Server;
using OpenAuth.IdentityServer.Handlers;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenAuth.IdentityServer
{
    public class Startup
    {
        public IHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration, IHostEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            // Cookie 认证（用于登录页面维持会话）
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                });

            // OpenIddict 配置
            services.AddOpenIddict()
                .AddServer(options =>
                {
                    // 启用授权码流程 + PKCE（Vue3 前端使用）
                    options.AllowAuthorizationCodeFlow()
                           .RequireProofKeyForCodeExchange();

                    // 启用隐式流程（Swagger 使用）
                    options.AllowImplicitFlow();

                    // 配置端点 URI
                    options.SetAuthorizationEndpointUris("/connect/authorize")
                           .SetTokenEndpointUris("/connect/token")
                           .SetUserinfoEndpointUris("/connect/userinfo")
                           .SetLogoutEndpointUris("/connect/logout");

                    // 注册 scope
                    options.RegisterScopes(Scopes.OpenId, Scopes.Profile, "openauthapi");

                    // 开发环境使用临时证书
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();

                    // 禁用 access_token 加密，让 WebApi 的 JwtBearer 能直接验证
                    options.DisableAccessTokenEncryption();

                    // 禁用降级模式下的强制 scope/client 验证
                    options.UseAspNetCore()
                           .EnableAuthorizationEndpointPassthrough()
                           .EnableTokenEndpointPassthrough()
                           .EnableUserinfoEndpointPassthrough()
                           .EnableLogoutEndpointPassthrough()
                           .DisableTransportSecurityRequirement(); // 开发环境允许 HTTP（生产环境应移除）

                    // 降级模式：不使用数据库存储，手动处理验证
                    options.EnableDegradedMode();

                    // 注册自定义验证 handler（降级模式必须）
                    options.AddEventHandler<ValidateAuthorizationRequestContext>(builder =>
                        builder.UseSingletonHandler<ValidateAuthorizationRequestHandler>());
                    options.AddEventHandler<ValidateTokenRequestContext>(builder =>
                        builder.UseSingletonHandler<ValidateTokenRequestHandler>());
                    options.AddEventHandler<ValidateLogoutRequestContext>(builder =>
                        builder.UseSingletonHandler<ValidateLogoutRequestHandler>());
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });
            
            services.ConfigureNonBreakingSameSiteCookies();
            
            services.AddCors();

            services.AddAuthentication();
            
            //映射配置文件
            services.Configure<AppSetting>(Configuration.GetSection("AppSetting"));
            
            //在startup里面只能通过这种方式获取到appsettings里面的值，不能用IOptions😰
            var dbtypes = ((ConfigurationSection)Configuration.GetSection("AppSetting:DbTypes")).GetChildren()
                .ToDictionary(x => x.Key, x => x.Value);
            var dbType = dbtypes[Define.DEFAULT_TENANT_ID];
            var connectionString = Configuration.GetConnectionString(Define.DEFAULT_TENANT_ID);
            if (dbType == Define.DBTYPE_SQLSERVER)
            {
                services.AddDbContext<OpenAuthDBContext>(options =>
                    options.UseSqlServer(connectionString));
            }
            else if(dbType == Define.DBTYPE_MYSQL) //mysql
            {
                services.AddDbContext<OpenAuthDBContext>(options =>
                    options.UseMySql(connectionString,new MySqlServerVersion(new Version(8, 0, 11))));
            }
            else  //oracle
            {
                services.AddDbContext<OpenAuthDBContext>(options =>
                    options.UseOracle(connectionString, o=>o.UseOracleSQLCompatibility("11")));
            }
            
            var sqlsugarTypes = UtilMethods.EnumToDictionary<SqlSugar.DbType>();
            var sugarDbtype = sqlsugarTypes.FirstOrDefault(it =>
                dbtypes.ToDictionary(u => u.Key, v => v.Value.ToLower()).ContainsValue(it.Key));

            services.AddScoped<ISqlSugarClient>(s =>
            {
                var sqlSugar = new SqlSugarClient(new ConnectionConfig()
                {
                    DbType = sugarDbtype.Value,
                    ConnectionString = connectionString,
                    IsAutoCloseConnection = true
                });

                 if(sugarDbtype.Value != SqlSugar.DbType.PostgreSQL){
                    return sqlSugar;
                }
                // 配置bool类型转换为smallint
                sqlSugar.Aop.OnExecutingChangeSql = (sql, parameters) =>
                {
                    foreach (var param in parameters)
                    {
                        if (param.Value is bool boolValue)
                        {
                            param.DbType = System.Data.DbType.Int16;
                            // 将 bool 转换为 smallint
                            param.Value = boolValue ? (short)1 : (short)0;
                        }
                    }
                    // 返回修改后的 SQL 和参数
                    return new System.Collections.Generic.KeyValuePair<string, SugarParameter[]>(sql, parameters);
                };
                return sqlSugar;
            });

        }
        
        public void ConfigureContainer(ContainerBuilder builder)
        {
            AutofacExt.InitAutofac(builder);
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            
            app.UseCookiePolicy();
            
            //todo:测试可以允许任意跨域，正式环境要加权限
            app.UseCors(builder => builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}