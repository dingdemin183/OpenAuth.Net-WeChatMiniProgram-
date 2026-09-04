using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.WxPay;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Threading.Tasks;

namespace OpenAuth.App.UserProfile
{
    /// <summary>
    /// 用户个人资料管理
    /// </summary>
    public class UserProfileApp
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuth _auth;
        private readonly WxSecurityService _wxSecurityService;

        public UserProfileApp(
            ISqlSugarClient db,
            IAuth auth,
            WxSecurityService wxSecurityService)
        {
            _db = db;
            _auth = auth;
            _wxSecurityService = wxSecurityService;
        }

        /// <summary>
        ///  更新用户个人资料（头像+昵称）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<UserProfileResp> UpdateProfileAsync(UpdateUserProfileReq request)
        {
            if (request == null)
                throw new CommonException("请求参数不能为空");

            // 获取当前登录用户
            var session= _auth.GetCurrentSession();

            if (session?.UserId == null)
            {
                throw new CommonException("用户未登录");
            }

            var userId = session.UserId;

            // 查询用户第三方认证信息
            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == userId && !x.IsDeleted)
                .ConfigureAwait(false); 

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            // 内容安全检测
            var openId = userAuth.OpenId;

            // 检测昵称
            if (!string.IsNullOrEmpty(request.NickName))
            {
                var isTextSafe = await _wxSecurityService.CheckTextSecurityAsync(openId, request.NickName);
                if (!isTextSafe)
                {
                    throw new CommonException("昵称包含违规内容，请修改");
                }
            }

            // 检测头像
            if (!string.IsNullOrEmpty(request.AvatarUrl))
            {
                var isImageSafe = await _wxSecurityService.CheckImageSecurityAsync(openId, request.AvatarUrl);
                if (!isImageSafe)
                {
                    throw new CommonException("头像包含违规内容，请更换");
                }
            }

            // 更新用户资料
            if (!string.IsNullOrEmpty(request.NickName))
            {
                userAuth.NickName = request.NickName;
            }

            if (!string.IsNullOrEmpty(request.AvatarUrl))
            {
                userAuth.AvatarUrl = request.AvatarUrl;
            }

            userAuth.UpdateTime = DateTime.Now;

            await _db.Updateable(userAuth)
                .UpdateColumns(x => new { x.NickName, x.AvatarUrl, x.UpdateTime })
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            // 返回更新后的资料
            return new UserProfileResp
            {
                Id = userAuth.Id,
                OpenId = userAuth.OpenId,
                NickName = userAuth.NickName,
                AvatarUrl = userAuth.AvatarUrl,
                UserPhone = userAuth.UserPhone
            };
        }

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<UserProfileResp> GetMyProfileAsync()
        {
            var session= _auth.GetCurrentSession();
            if(session?.UserId == null)
            {
                throw new CommonException("用户未登录");
            }

            var userId = session?.UserId;

            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == userId && !x.IsDeleted)
                .ConfigureAwait(false); 

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            return new UserProfileResp
            {
                Id = userAuth.Id,
                OpenId = userAuth.OpenId,
                NickName = userAuth.NickName,
                AvatarUrl = userAuth.AvatarUrl,
                UserPhone = userAuth.UserPhone
            };
        }
    }
}