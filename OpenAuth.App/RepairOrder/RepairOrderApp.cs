using Infrastructure;
using Microsoft.Extensions.Configuration.UserSecrets;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.SSO;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OpenAuth.App.Repair
{
    /// <summary>
    /// 保修单业务
    /// </summary>
    public class RepairOrderApp : SqlSugarBaseApp<RepairOrder>
    {
        private readonly ISqlSugarClient _db;

        public RepairOrderApp(ISqlSugarClient client, IAuth auth) : base(client, auth)
        {
            _db = client;
        }

        #region 后台管理 报修管理

        /// <summary>
        /// 分页查询报修列表（后台管理）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        public async Task<TableResp<RepairOrderResp>> QueryAsync(QueryRepairOrderListReq request)
        {
            var query = _db.Queryable<RepairOrder>()
                .Where(x => !x.IsDeleted);

            // 状态筛选
            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            // 手机号模糊搜索
            if (!string.IsNullOrEmpty(request.Phone))
            {
                query = query.Where(x => x.Phone.Contains(request.Phone));
            }

            // 姓名模糊搜索
            if (!string.IsNullOrEmpty(request.UserName))
            {
                query = query.Where(x => x.UserName.Contains(request.UserName));
            }

            // 关键词搜索（手机号或姓名）
            if (!string.IsNullOrEmpty(request.key))
            {
                query = query.Where(x => x.Phone.Contains(request.key) || x.UserName.Contains(request.key));
            }

            // 时间范围筛选
            if (request.StartTime.HasValue)
            {
                query = query.Where(x => x.CreateTime >= request.StartTime.Value);
            }
            if (request.EndTime.HasValue)
            {
                var endTime = request.EndTime.Value.Date.AddDays(1);
                query = query.Where(x => x.CreateTime < endTime);
            }

            // 总记录数
            var total = await query.CountAsync();

            // 分页
            var list = await query
                .OrderByDescending(x => x.CreateTime)
                .Skip((request.page - 1) * request.limit)
                .Take(request.limit)
                .ToListAsync();

            // 转换为 DTO
            var respList = list.Select(x => new RepairOrderResp
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                Phone = x.Phone,
                ProductBrand = x.ProductBrand,
                ProductType = x.ProductType,
                ProductModel = x.ProductModel,
                FaultDesc = x.FaultDesc,
                PurchaseDate = x.PurchaseDate,
                EnergyImage = x.EnergyImage,
                ProductImage = x.ProductImage,
                TradeImage = x.TradeImage,
                Province = x.Province,
                City = x.City,
                Area = x.Area,
                DetailAddress = x.DetailAddress,
                Status = x.Status,
                StatusText = GetStatusText(x.Status),
                Remark = x.Remark,
                HandlerId = x.HandlerId,
                HandledTime = x.HandledTime,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime
            }).ToList();

            return new TableResp<RepairOrderResp>
            {
                Data = respList,
                Count = total,
                Page = request.page,
                Limit = request.limit
            };
        }

        /// <summary>
        /// 根据id获取报修单详情
        /// </summary>
        /// <param name="id">报修单id</param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task<RepairOrderResp> GetDetailAsync(string id)
        {
            var entity = await _db.Queryable<RepairOrder>()
                .FirstAsync(x => x.Id == id && !x.IsDeleted);

            if (entity == null)
            {
                throw new CommonException("报修单不存在");
            }

            return new RepairOrderResp
            {
                Id = entity.Id,
                UserId = entity.UserId,
                UserName = entity.UserName,
                Phone = entity.Phone,
                ProductBrand = entity.ProductBrand,
                ProductType = entity.ProductType,
                ProductModel = entity.ProductModel,
                FaultDesc = entity.FaultDesc,
                PurchaseDate = entity.PurchaseDate,
                EnergyImage = entity.EnergyImage,
                ProductImage = entity.ProductImage,
                TradeImage = entity.TradeImage,
                Province = entity.Province,
                City = entity.City,
                Area = entity.Area,
                DetailAddress = entity.DetailAddress,
                Status = entity.Status,
                StatusText = GetStatusText(entity.Status),
                Remark = entity.Remark,
                HandlerId = entity.HandlerId,
                HandledTime = entity.HandledTime,
                CreateTime = entity.CreateTime,
                UpdateTime = entity.UpdateTime
            };
        }

        /// <summary>
        /// 获取报修单总数
        /// </summary>
        /// <returns>报修单总数量</returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<int> GetRepairOrderCountAsync()
        {
            var count = await _db.Queryable<RepairOrder>()
                   .Where(t => t.IsDeleted == false)
                   .CountAsync()
                   .ConfigureAwait(false);

            return count;
        }

       


        /// <summary>
        /// 后台管理 - 审核报修
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <param name="handlerId">处理人id</param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task UpdateStatusAsync(UpdateRepairStatusReq request, string handlerId)
        {
            // 校验参数
            if (request.Status < 0 || request.Status >= 2)
            {
                throw new CommonException("无效的状态值");
            }

            if (request.Status == 0 && string.IsNullOrEmpty(request.Remark))
            {
                throw new CommonException("请填写拒绝理由");
            }
            var entity = await _db.Queryable<RepairOrder>()
                .FirstAsync(x => x.Id == request.Id && !x.IsDeleted)
                .ConfigureAwait(false);

            if (entity == null)
            {
                throw new CommonException("报修单不存在");
            }
            if (entity.Status != 2)
            {
                throw new CommonException("当前状态不能审核报修");
            }

            entity.Status = request.Status;
            entity.Remark = request.Remark;
            entity.HandlerId = handlerId;
            entity.HandledTime = DateTime.Now;
            entity.UpdateTime = DateTime.Now;

            var result = await _db.Updateable(entity)
                .UpdateColumns(x => new { x.Status, x.Remark, x.HandlerId, x.HandledTime, x.UpdateTime })
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

        }

       
        /// <summary>
        /// 删除报修（软删除）
        /// </summary>
        public async Task DeleteAsync(string id)
        {
            var entity = await _db.Queryable<RepairOrder>()
                                  .FirstAsync(x => x.Id == id && !x.IsDeleted)
                                  .ConfigureAwait(false);

            if (entity == null)
            {
                throw new CommonException("报修单不存在");
            }
            try
            {
                entity.IsDeleted = true;
                entity.UpdateTime = DateTime.Now;

                var result = await _db.Updateable(entity)
                                      .UpdateColumns(x => new { x.IsDeleted, x.UpdateTime })
                                      .ExecuteCommandAsync()
                                      .ConfigureAwait(false);

                if (result <= 0)
                {
                    throw new CommonException("删除失败");
                }

            }
            catch (CommonException ex)
            {
                throw new CommonException($"删除失败: {ex.Message}");
            }

        }

        /// <summary>
        /// 获取状态文本
        /// </summary>
        private string GetStatusText(int status)
        {
            return status switch
            {
                0 => "拒绝报修",
                1 => "同意报修",
                2 => "未处理",
                _ => "未知"
            };
        }

        #endregion 后台管理

        #region  小程序端

        /// <summary>
        /// 小程序用户提交报修
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<string> SubmitAsync(AddRepairOrderReq request)
        {

            // 校验必填字段
            if (string.IsNullOrEmpty(request.UserName))
                throw new CommonException("请填写姓名");
            if (string.IsNullOrEmpty(request.Phone))
                throw new CommonException("请填写手机号码");
            if (string.IsNullOrEmpty(request.ProductBrand))
                throw new CommonException("请填写产品品牌");
            if (string.IsNullOrEmpty(request.ProductType))
                throw new CommonException("请选择产品类型");
            if (string.IsNullOrEmpty(request.ProductModel))
                throw new CommonException("请填写产品型号");
            if (string.IsNullOrEmpty(request.FaultDesc))
                throw new CommonException("请填写故障描述");
            if (string.IsNullOrEmpty(request.DetailAddress))
                throw new CommonException("请填写详细地址");
            var session = _auth.GetCurrentSession();
            if (string.IsNullOrEmpty(session.UserId))
            {
                throw new CommonException("用户未登录，请登录");
            }
            var entity = new RepairOrder
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = "1",
                UserName = request.UserName,
                Phone = request.Phone,
                ProductBrand = request.ProductBrand,
                ProductType = request.ProductType,
                ProductModel = request.ProductModel,
                FaultDesc = request.FaultDesc,
                PurchaseDate = request.PurchaseDate,
                EnergyImage = request.EnergyImage,
                ProductImage = request.ProductImage,
                TradeImage = request.TradeImage,
                Province = request.Province,
                City = request.City,
                Area = request.Area,
                DetailAddress = request.DetailAddress,
                Status = 2, // 未报修
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            var result = await _db.Insertable(entity).ExecuteCommandAsync();
            if (result <= 0)
            {
                throw new CommonException("提交报修失败");
            }
            return entity.Id;


        }

        /// <summary>
        /// 小程序端查询当前用户的保修列表
        /// </summary>
        /// <param name="request">查询参数</param>
        /// <returns></returns>
        public async Task<TableResp<RepairOrderResp>> QueryByUserAsync(QueryRepairOrderListReq request)
        {
            var session = _auth.GetCurrentSession();
            var userId = session.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                throw new CommonException("当前用户未登录，请重新登录");
            }
            var query = _db.Queryable<RepairOrder>()
                .Where(x => !x.IsDeleted && x.UserId == userId);

            // 状态筛选
            if (request.Status.HasValue)
            {
                query = query.Where(x => x.Status == request.Status.Value);
            }

            // 时间范围筛选
            if (request.StartTime.HasValue)
            {
                query = query.Where(x => x.CreateTime >= request.StartTime.Value);
            }
            if (request.EndTime.HasValue)
            {
                var endTime = request.EndTime.Value.Date.AddDays(1);
                query = query.Where(x => x.CreateTime < endTime);
            }

            // 关键词搜索（故障描述或产品型号）
            if (!string.IsNullOrEmpty(request.key))
            {
                query = query.Where(x => x.FaultDesc.Contains(request.key)
                                          || x.ProductModel.Contains(request.key)
                                          || x.ProductBrand.Contains(request.key)
                                          || x.ProductType.Contains(request.key));
            }

            var total = await query.CountAsync();

            var list = await query
                .OrderByDescending(x => x.CreateTime)
                .Skip((request.page - 1) * request.limit)
                .Take(request.limit)
                .ToListAsync();

            var respList = list.Select(x => new RepairOrderResp
            {
                Id = x.Id,
                UserId = x.UserId,
                UserName = x.UserName,
                Phone = x.Phone,
                ProductBrand = x.ProductBrand,
                ProductType = x.ProductType,
                ProductModel = x.ProductModel,
                FaultDesc = x.FaultDesc,
                PurchaseDate = x.PurchaseDate,
                EnergyImage = x.EnergyImage,
                ProductImage = x.ProductImage,
                TradeImage = x.TradeImage,
                Province = x.Province,
                City = x.City,
                Area = x.Area,
                DetailAddress = x.DetailAddress,
                Status = x.Status,
                StatusText = GetStatusText(x.Status),
                Remark = x.Remark,
                HandlerId = x.HandlerId,
                HandledTime = x.HandledTime,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime
            }).ToList();

            return new TableResp<RepairOrderResp>
            {
                Data = respList,
                Count = total,
                Page = request.page,
                Limit = request.limit
            };
        }

        /// <summary>
        /// 小程序用户更新报修
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<string> UpdateAsync(UpdateRepairOrderReq request)
        {
            // 校验必填字段
            if (string.IsNullOrEmpty(request.UserName))
                throw new CommonException("请填写姓名");
            if (string.IsNullOrEmpty(request.Phone))
                throw new CommonException("请填写手机号码");
            if (string.IsNullOrEmpty(request.ProductBrand))
                throw new CommonException("请填写产品品牌");
            if (string.IsNullOrEmpty(request.ProductType))
                throw new CommonException("请选择产品类型");
            if (string.IsNullOrEmpty(request.ProductModel))
                throw new CommonException("请填写产品型号");
            if (string.IsNullOrEmpty(request.FaultDesc))
                throw new CommonException("请填写故障描述");
            if (string.IsNullOrEmpty(request.DetailAddress))
                throw new CommonException("请填写详细地址");
            if (string.IsNullOrEmpty(request.Id))
                throw new CommonException("请提供报修单ID");

            // 校验登录信息
            var session = _auth.GetCurrentSession();
            if (string.IsNullOrEmpty(session.UserId))
            {
                throw new CommonException("用户未登录");
            }

            // 查询报修单是否存在且属于当前用户
            var existingRepair = await _db.Queryable<RepairOrder>()
                .Where(t => t.Id == request.Id && t.UserId == session.UserId && t.IsDeleted == false)
                .FirstAsync()
                .ConfigureAwait(false);

            if (existingRepair == null)
            {
                throw new CommonException("未找到报修单信息或无权限修改");
            }

            // 更新实体
            existingRepair.UserName = request.UserName;
            existingRepair.Phone = request.Phone;
            existingRepair.ProductBrand = request.ProductBrand;
            existingRepair.ProductType = request.ProductType;
            existingRepair.ProductModel = request.ProductModel;
            existingRepair.FaultDesc = request.FaultDesc;
            existingRepair.PurchaseDate = request.PurchaseDate;
            existingRepair.EnergyImage = request.EnergyImage;
            existingRepair.ProductImage = request.ProductImage;
            existingRepair.TradeImage = request.TradeImage;
            existingRepair.Province = request.Province;
            existingRepair.City = request.City;
            existingRepair.Area = request.Area;
            existingRepair.DetailAddress = request.DetailAddress;
            existingRepair.UpdateTime = DateTime.Now;

            var result = await _db.Updateable(existingRepair)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            if (result <= 0)
            {
                throw new CommonException("更新报修失败");
            }

            return existingRepair.Id;
        }


        #endregion
    }
}