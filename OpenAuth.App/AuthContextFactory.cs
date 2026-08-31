// ***********************************************************************
// Assembly         : OpenAuth.App
// Author           : 李玉宝
// Created          : 07-05-2018
//
// Last Modified By : 李玉宝
// Last Modified On : 07-05-2018
// ***********************************************************************
// <copyright file="AuthContextFactory.cs" company="OpenAuth.App">
//     Copyright (c) http://www.openauth.net.cn. All rights reserved.
// </copyright>
// <summary>
// 用户权限策略工厂
//</summary>
// ***********************************************************************

using Infrastructure;
using OpenAuth.Repository;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Interface;
using SqlSugar;
using Infrastructure.Extensions.AutofacManager;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Http;

namespace OpenAuth.App
{
    /// <summary>
    ///  加载用户所有可访问的资源/机构/模块
    /// <para>李玉宝新增于2016-07-19 10:53:30</para>
    /// </summary>
    public class AuthContextFactory
    {
        private SystemAuthStrategy _systemAuth;
        private NormalAuthStrategy _normalAuthStrategy;
        
        protected ISqlSugarClient SugarClient;

        public AuthContextFactory(SystemAuthStrategy sysStrategy
            , NormalAuthStrategy normalAuthStrategy
            , ISqlSugarClient client)
        {
            _systemAuth = sysStrategy;
            _normalAuthStrategy = normalAuthStrategy;
            string tenantId = Define.DEFAULT_TENANT_ID;
            var httpContextAccessor = AutofacContainerModule.GetService<IHttpContextAccessor>();
            if (httpContextAccessor != null)
            {
                tenantId = httpContextAccessor.GetTenantId();
            }
            
            if(tenantId != Define.DEFAULT_TENANT_ID) //如果不是默认租户，则使用租户id获取连接
            {
                client = client.AsTenant().GetConnection(tenantId);
            }
            
            SugarClient = client;
        }

        public AuthStrategyContext GetAuthStrategyContext(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;

            IAuthStrategy service = null;
             if (username == Define.SYSTEM_USERNAME)
            {
                service= _systemAuth;
            }
            else
            {
                service = _normalAuthStrategy;
                service.User = SugarClient.Queryable<SysUser>().First(u => u.Account == username);
            }

         return new AuthStrategyContext(service);
        }
    }
}