using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using SqlSugar;


namespace OpenAuth.App
{
    public class CategoryTypeApp : SqlSugarBaseApp<CategoryType>
    {
        private RevelanceManagerApp _revelanceApp;

        /// <summary>
        /// 加载列表
        /// </summary>
        public async Task<PagedDynamicDataResp> Load(QueryCategoryTypeListReq request)
        {
            var result = new PagedDynamicDataResp();
            var objs = SugarClient.Queryable<CategoryType>();
            if (!string.IsNullOrEmpty(request.key))
            {
                objs = objs.Where(u => u.Id.Contains(request.key) || u.Name.Contains(request.key));
            }
            
            result.Data =await objs.OrderBy(u => u.Name)
                .Skip((request.page - 1) * request.limit)
                .Take(request.limit).ToListAsync();
            result.Count =await objs.CountAsync();
            return result;
        }

        public void Add(AddOrUpdateCategoryTypeReq req)
        {
            var obj = req.MapTo<CategoryType>();
            //todo:补充或调整自己需要的字段
            obj.CreateTime = DateTime.Now;
            Repository.Insert(obj);
        }

         public void Update(AddOrUpdateCategoryTypeReq obj)
        {
            var user = _auth.GetCurrentUser().User;
            Repository.Update(u => new CategoryType
            {
                Name = obj.Name,
                CreateTime = DateTime.Now
                //todo:补充或调整自己需要的字段
            }, u => u.Id == obj.Id);

        }
         
         public new void Delete(string[] ids)
         {
             SugarClient.Ado.BeginTran();
             SugarClient.Deleteable<CategoryType>().Where(u=>ids.Contains(u.Id)).ExecuteCommand();
             SugarClient.Deleteable<Category>().Where(u=>ids.Contains(u.TypeId)).ExecuteCommand();
             SugarClient.Ado.CommitTran();
          
         }
         
         public List<CategoryType> AllTypes()
         {
             return SugarClient.Queryable<CategoryType>().ToList();
         }

        public CategoryTypeApp(ISqlSugarClient client,
            RevelanceManagerApp app, IAuth auth) : base(client, auth)
        {
            _revelanceApp = app;
        }
    }
}