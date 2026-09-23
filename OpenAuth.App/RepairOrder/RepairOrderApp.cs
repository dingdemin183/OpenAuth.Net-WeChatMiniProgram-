using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.SSO;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Collections.Generic;
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
            if (request == null)
                throw new CommonException("请求参数不能为空");
            if (request.page < 1)
                throw new CommonException("页码必须大于 0");
            if (request.limit < 1 || request.limit > 200)
                throw new CommonException("每页数量必须在 1-200 之间");

            var query = _db.Queryable<RepairOrder>()
                .Where(x => x.IsDeleted == false);

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
                .ToPageListAsync(request.page, request.limit);

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
                .FirstAsync(x => x.Id == id && x.IsDeleted == false);

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
                HandledTime = entity.HandledTime,
                CreateTime = entity.CreateTime,
                UpdateTime = entity.UpdateTime
            };
        }

        /// <summary>
        /// 获取报修人总数
        /// </summary>
        /// <returns>报修单总数量</returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<int> GetRepairOrderCountAsync()
        {
            var count = await _db.Queryable<RepairOrder>()
                .Where(t => t.IsDeleted == false)
                .GroupBy(t => t.UserId)
                .Select(t => t.UserId)
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
                .FirstAsync(x => x.Id == request.Id && x.IsDeleted == false)
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

            // 只把数据库写操作包在事务里
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Updateable(entity)
                    .UpdateColumns(x => new { x.Status, x.Remark, x.HandlerId, x.HandledTime, x.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
        }

       
        /// <summary>
        /// 删除报修（软删除）
        /// </summary>
        public async Task DeleteAsync(string id)
        {
            var entity = await _db.Queryable<RepairOrder>()
                                  .FirstAsync(x => x.Id == id && x.IsDeleted == false)
                                  .ConfigureAwait(false);

            if (entity == null)
            {
                throw new CommonException("报修单不存在");
            }

            entity.IsDeleted = true;
            entity.UpdateTime = DateTime.Now;

            // 只把数据库写操作包在事务里
            await _db.Ado.BeginTranAsync();
            try
            {
                var result = await _db.Updateable(entity)
                                      .UpdateColumns(x => new { x.IsDeleted, x.UpdateTime })
                                      .ExecuteCommandAsync()
                                      .ConfigureAwait(false);

                if (result <= 0)
                {
                    await _db.Ado.RollbackTranAsync();
                    throw new CommonException("删除失败");
                }

                await _db.Ado.CommitTranAsync();
            }
            catch (CommonException)
            {
                // 业务校验异常已经回滚过事务，直接抛出，避免重复 Rollback 报错
                throw;
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
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
                UserId = session.UserId,
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
                Status = 2, // 未处理（待审核）
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            // 只把数据库写操作包在事务里
            await _db.Ado.BeginTranAsync();
            try
            {
                var result = await _db.Insertable(entity)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (result <= 0)
                {
                    await _db.Ado.RollbackTranAsync();
                    throw new CommonException("提交报修失败");
                }

                await _db.Ado.CommitTranAsync();
            }
            catch (CommonException)
            {
                throw;
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
            return entity.Id;
        }

        /// <summary>
        /// 小程序端查询当前用户的保修列表
        /// </summary>
        /// <param name="request">查询参数</param>
        /// <returns></returns>
        public async Task<List<RepairOrderResp>> QueryByUserAsync()
        {
            var session = _auth.GetCurrentSession();
            var userId = session.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                throw new CommonException("当前用户未登录，请重新登录");
            }

            // 查询用户的所有报修单（按创建时间倒序）
            var repairOrders = await _db.Queryable<RepairOrder>()
                .Where(x => x.IsDeleted == false && x.UserId == userId)
                .OrderByDescending(x => x.CreateTime)
                .ToListAsync()
                .ConfigureAwait(false);

            // 映射到响应DTO列表
            return repairOrders.Select(order => new RepairOrderResp
            {
                Id = order.Id,
                UserId = order.UserId,
                UserName = order.UserName,
                Phone = order.Phone,
                ProductBrand = order.ProductBrand,
                ProductType = order.ProductType,
                ProductModel = order.ProductModel,
                FaultDesc = order.FaultDesc,
                PurchaseDate = order.PurchaseDate,
                EnergyImage = order.EnergyImage,
                ProductImage = order.ProductImage,
                TradeImage = order.TradeImage,
                Province = order.Province,
                City = order.City,
                Area = order.Area,
                DetailAddress = order.DetailAddress,
                Status = order.Status,
                StatusText = GetStatusText(order.Status),
                Remark = order.Remark,
                HandledTime = order.HandledTime,
                CreateTime = order.CreateTime,
                UpdateTime = order.UpdateTime
            }).ToList();
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

            // 已审核的报修单不允许用户修改（避免覆盖审核字段 Status/HandlerId/HandledTime）
            if (existingRepair.Status != 2)
            {
                throw new CommonException("报修单已审核，不允许修改");
            }

            // 更新实体（仅用户可改的字段，审核相关字段保持不变）
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

            // 只把数据库写操作包在事务里，且用 UpdateColumns 显式限定字段
            // 避免把 Status/HandlerId/HandledTime 等审核字段也写回数据库造成覆盖
            await _db.Ado.BeginTranAsync();
            try
            {
                var result = await _db.Updateable(existingRepair)
                    .UpdateColumns(x => new
                    {
                        x.UserName,
                        x.Phone,
                        x.ProductBrand,
                        x.ProductType,
                        x.ProductModel,
                        x.FaultDesc,
                        x.PurchaseDate,
                        x.EnergyImage,
                        x.ProductImage,
                        x.TradeImage,
                        x.Province,
                        x.City,
                        x.Area,
                        x.DetailAddress,
                        x.UpdateTime
                    })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (result <= 0)
                {
                    await _db.Ado.RollbackTranAsync();
                    throw new CommonException("更新报修失败");
                }

                await _db.Ado.CommitTranAsync();
            }
            catch (CommonException)
            {
                throw;
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }

            return existingRepair.Id;
        }


        #endregion
    }
}