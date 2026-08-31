using System.Threading.Tasks;
using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using SqlSugar;


namespace OpenAuth.App
{
    public class FrmLeaveReqApp : SqlSugarBaseApp<FrmLeaveReq>, ICustomerForm
    {
        private RevelanceManagerApp _revelanceApp;

        /// <summary>
        /// 加载列表
        /// </summary>
        public async Task<PagedDynamicDataResp> Load(QueryFrmLeaveReqListReq request)
        {
             return new PagedDynamicDataResp
            {
                Count = await SugarClient.Queryable<FrmLeaveReq>().CountAsync(),
                Data = await SugarClient.Queryable<FrmLeaveReq>().OrderByDescending(u => u.Id)
                    .Skip((request.page - 1) * request.limit)
                    .Take(request.limit).ToListAsync()
            };
        }

        public void Add(FrmLeaveReq obj)
        {
            Repository.Insert(obj);
        }
        
        public FrmLeaveReqApp(ISqlSugarClient client,
            RevelanceManagerApp app,IAuth auth) : base(client, auth)
        {
            _revelanceApp = app;
        }

        public void Add(string flowInstanceId, string frmData)
        {
            var req = JsonHelper.Instance.Deserialize<FrmLeaveReq>(frmData);
            req.FlowInstanceId = flowInstanceId;
            Add(req);
        }

        public void Update(string flowInstanceId, string frmData)
        {
            var req = JsonHelper.Instance.Deserialize<FrmLeaveReq>(frmData);
            Repository.Update(u => new FrmLeaveReq
            {
                UserName = req.UserName,
                RequestComment = req.RequestComment,
                RequestType = req.RequestType
                //补充其他需要更新的字段
            }, u => u.FlowInstanceId == flowInstanceId);
        }
    }
}