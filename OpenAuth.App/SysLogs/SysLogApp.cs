using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using SqlSugar;


namespace OpenAuth.App
{
    public class SysLogApp : SqlSugarBaseApp<SysLog>
    {

        /// <summary>
        /// 加载列表
        /// </summary>
        public async Task<PagedDynamicDataResp> Load(QuerySysLogListReq request)
        {
            var result = new PagedDynamicDataResp();
            var objs = SugarClient.Queryable<SysLog>();
            if (!string.IsNullOrEmpty(request.key))
            {
                objs = objs.Where(u => u.Content.Contains(request.key) || u.Id.Contains(request.key));
            }

            result.Data = await objs.OrderByDescending(u => u.CreateTime)
                .Skip((request.page - 1) * request.limit)
                .Take(request.limit).ToListAsync();
            result.Count = await objs.CountAsync();
            return result;
        }

        public void Add(SysLog obj)
        {
            //程序类型取入口应用的名称，可以根据自己需要调整
            obj.Application = Assembly.GetEntryAssembly().FullName.Split(',')[0];
            Repository.Insert(obj);
        }
        
        public void Update(SysLog obj)
        {
            Repository.Update(u => new SysLog
            {
               //todo:要修改的字段赋值
            }, u => u.Id == obj.Id);

        }

        public SysLogApp(ISqlSugarClient client) : base(client, null)
        {
        }
    }
}