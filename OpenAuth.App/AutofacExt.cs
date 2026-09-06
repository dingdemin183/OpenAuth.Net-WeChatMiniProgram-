// ***********************************************************************
// Assembly         : OpenAuth.Mvc
// Author           : yubaolee
// Created          : 10-26-2015
//
// Last Modified By : yubaolee
// Last Modified On : 10-26-2015
// ***********************************************************************
// <copyright file="AutofacExt.cs" company="www.cnblogs.com/yubaolee">
//     Copyright (c) www.cnblogs.com/yubaolee. All rights reserved.
// </copyright>
// <summary>IOC扩展</summary>
// ***********************************************************************

using Autofac;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.Quartz;
using Infrastructure;
using Infrastructure.Cache;
using Infrastructure.Extensions.AutofacManager;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Options;
using OpenAuth.App.Interface;
using OpenAuth.App.SSO;
using OpenAuth.App.WxPay;
using OpenAuth.Repository;
using OpenAuth.Repository.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using IContainer = Autofac.IContainer;

namespace OpenAuth.App
{
    public static class AutofacExt
    {
        private static IContainer _container;

        public static IContainer InitForTest(IServiceCollection services)
        {
            var builder = new ContainerBuilder();

            //注册数据库基础操作和工作单元
            services.AddScoped(typeof(IRepository<,>), typeof(BaseRepository<,>));
            services.AddScoped(typeof(IUnitWork<>), typeof(UnitWork<>));

            //注入授权
            builder.RegisterType(typeof(LocalAuth)).As(typeof(IAuth));

            //注册app层
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly());

            //防止单元测试时已经注入
            if (services.All(u => u.ServiceType != typeof(ICacheContext)))
            {
                services.AddScoped(typeof(ICacheContext), typeof(CacheContext));
            }

            if (services.All(u => u.ServiceType != typeof(IHttpContextAccessor)))
            {
                services.AddScoped(typeof(IHttpContextAccessor), typeof(HttpContextAccessor));
            }

            InitDependency(builder);

            // 注册 WeChatPayV3Signer
            RegisterWeChatPaySigner(builder);

            builder.RegisterModule(new QuartzAutofacFactoryModule());

            builder.Populate(services);

            _container = builder.Build();
            return _container;
        }

        public static void InitAutofac(ContainerBuilder builder)
        {
            //注册数据库基础操作和工作单元
            builder.RegisterGeneric(typeof(BaseRepository<,>)).As(typeof(IRepository<,>));
            builder.RegisterGeneric(typeof(UnitWork<>)).As(typeof(IUnitWork<>));
            //注入授权
            builder.RegisterType(typeof(LocalAuth)).As(typeof(IAuth)).InstancePerLifetimeScope();

            //注册app层（排除 WeChatPayV3Signer）
            builder.RegisterAssemblyTypes(Assembly.GetExecutingAssembly())
                   .Where(t => t != typeof(WeChatPayV3Signer));

            builder.RegisterType(typeof(CacheContext)).As(typeof(ICacheContext));
            builder.RegisterType(typeof(HttpContextAccessor)).As(typeof(IHttpContextAccessor));

            InitDependency(builder);

            // 注册 WeChatPayV3Signer
            RegisterWeChatPaySigner(builder);

            builder.RegisterModule(new QuartzAutofacFactoryModule());
        }

        /// <summary>
        /// 注册微信支付签名器
        /// </summary>
        private static void RegisterWeChatPaySigner(ContainerBuilder builder)
        {
            builder.Register(componentContext =>
            {
                var appSettingOptions = componentContext.Resolve<IOptions<AppSetting>>();
                var config = appSettingOptions.Value.WeChatPay;

                if (config == null)
                {
                    throw new InvalidOperationException(
                        "WeChatPay 配置未加载，请检查 appsettings.json 中 AppSetting:WeChatPay 节点是否存在"
                    );
                }

                // 检查必要配置
                if (string.IsNullOrEmpty(config.PrivateKeyPath))
                {
                    throw new InvalidOperationException(
                        "微信支付配置缺失: PrivateKeyPath 未设置，请检查 appsettings.json 中 AppSetting:WeChatPay:PrivateKeyPath"
                    );
                }
                if (string.IsNullOrEmpty(config.MchId))
                {
                    throw new InvalidOperationException(
                        "微信支付配置缺失: MchId 未设置，请检查 appsettings.json 中 AppSetting:WeChatPay:MchId"
                    );
                }
                if (string.IsNullOrEmpty(config.SerialNo))
                {
                    throw new InvalidOperationException(
                        "微信支付配置缺失: SerialNo 未设置，请检查 appsettings.json 中 AppSetting:WeChatPay:SerialNo"
                    );
                }

                return new WeChatPayV3Signer(
                    config.PrivateKeyPath,  // 参数1: 私钥路径
                    config.MchId,           // 参数2: 商户号
                    config.SerialNo         // 参数3: 证书序列号
                );
            })
            .AsSelf()
            .InstancePerLifetimeScope();
        }

        /// <summary>
        /// 注入所有继承了IDependency接口
        /// </summary>
        /// <param name="builder"></param>
        private static void InitDependency(ContainerBuilder builder)
        {
            Type baseType = typeof(IDependency);
            var compilationLibrary = DependencyContext.Default
                .CompileLibraries
                .Where(x => !x.Serviceable
                            && x.Type == "project")
                .ToList();
            var count1 = compilationLibrary.Count;
            List<Assembly> assemblyList = new List<Assembly>();

            foreach (var _compilation in compilationLibrary)
            {
                try
                {
                    assemblyList.Add(AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(_compilation.Name)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(_compilation.Name + ex.Message);
                }
            }

            builder.RegisterAssemblyTypes(assemblyList.ToArray())
                .Where(type => baseType.IsAssignableFrom(type) && !type.IsAbstract)
                .AsSelf().AsImplementedInterfaces()
                .InstancePerLifetimeScope();
        }
    }
}