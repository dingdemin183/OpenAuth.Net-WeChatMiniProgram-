using System;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using SqlSugar;


namespace OpenAuth.App
{
    public class FormApp : SqlSugarBaseApp<Form>
    {
        private IOptions<AppSetting> _appConfiguration;
        private IHttpContextAccessor _httpContextAccessor;
        /// <summary>
        /// 加载列表
        /// </summary>
        public async Task<PagedDynamicDataResp> Load(QueryFormListReq request)
        {
            var result = new PagedDynamicDataResp();
            var forms = GetDataPrivilege("u");
            if (!string.IsNullOrEmpty(request.key))
            {
                forms = forms.Where(u => u.Name.Contains(request.key) || u.Id.Contains(request.key));
            }

            result.Data = forms.OrderByDescending(u => u.CreateDate)
                .Skip((request.page - 1) * request.limit)
                .Take(request.limit).ToList();
            result.Count = await forms.CountAsync();
            return result;
        }

        public void Add(Form obj)
        {
            var user = _auth.GetCurrentUser().User;
            obj.CreateUserId = user.Id;
            obj.CreateUserName = user.Name;
            Repository.Insert(obj);
            if (!string.IsNullOrEmpty(obj.DbName))
            {
                var dbtype = _appConfiguration.Value.DbTypes[_httpContextAccessor.GetTenantId()];
                var sql = FormFactory.CreateForm(obj, SugarClient).GetSql(obj, dbtype);
                if (!string.IsNullOrEmpty(sql))
                {
                    SugarClient.Ado.ExecuteCommand(sql);
                }
            }
        }

        public void Update(Form obj)
        {
            Repository.Update(u => new Form
            {
                FrmType = obj.FrmType,
                ContentData = obj.ContentData,
                Content = obj.Content,
                ContentParse = obj.ContentParse,
                Name = obj.Name,
                Disabled = obj.Disabled,
                DbName = obj.DbName,
                SortCode = obj.SortCode,
                Description = obj.Description,
                OrgId =  obj.OrgId,
                ModifyDate = DateTime.Now
            }, u => u.Id == obj.Id);

            if (!string.IsNullOrEmpty(obj.DbName))
            {
                var dbtype = _appConfiguration.Value.DbTypes[_httpContextAccessor.GetTenantId()];
                var sql = FormFactory.CreateForm(obj, SugarClient).GetSql(obj, dbtype);
                if (!string.IsNullOrEmpty(sql))
                {
                    SugarClient.Ado.ExecuteCommand(sql);
                }
            }
        }

        public FormResp FindSingle(string id)
        {
            var form = Get(id);
            return form.MapTo<FormResp>();
        }

        public FormApp(ISqlSugarClient client,
            IAuth auth, IOptions<AppSetting> appConfiguration, IHttpContextAccessor httpContextAccessor) : base(client, auth)
        {
            _auth = auth;
            _appConfiguration = appConfiguration;
            _httpContextAccessor = httpContextAccessor;
        }
    }
}