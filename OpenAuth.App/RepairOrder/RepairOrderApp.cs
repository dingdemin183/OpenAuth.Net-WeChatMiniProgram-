using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OpenAuth.App.Repair
{
    /// <summary>
    /// 报修单业务逻辑
    /// </summary>
    public class RepairOrderApp : SqlSugarBaseApp<RepairOrder>
    {
        private readonly ISqlSugarClient _db;

        public RepairOrderApp(ISqlSugarClient client, IAuth auth) : base(client, auth)
        {
            _db = client;
        }

        /// <summary>
        /// 分页查询报修列表（后台管理）
        /// </summary>
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
                ProvinceId = x.ProvinceId,
                City = x.City,
                CityId = x.CityId,
                District = x.District,
                DistrictId = x.DistrictId,
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
        /// 获取报修单详情
        /// </summary>
        public async Task<RepairOrderResp> GetDetailAsync(string id)
        {
            var entity = await _db.Queryable<RepairOrder>()
                .FirstAsync(x => x.Id == id && !x.IsDeleted);

            if (entity == null)
            {
                throw new Exception("报修单不存在");
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
                ProvinceId = entity.ProvinceId,
                City = entity.City,
                CityId = entity.CityId,
                District = entity.District,
                DistrictId = entity.DistrictId,
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
        /// 小程序用户提交报修（新增）
        /// </summary>
        public async Task<string> SubmitAsync(AddOrUpdateRepairOrderReq request)
        {
            //if (string.IsNullOrEmpty(request.UserId))
            //{
            //    throw new Exception("用户未登录");
            //}
            // 校验必填字段
            if (string.IsNullOrEmpty(request.UserName))
                throw new Exception("请填写姓名");
            if (string.IsNullOrEmpty(request.Phone))
                throw new Exception("请填写手机号码");
            if (string.IsNullOrEmpty(request.ProductBrand))
                throw new Exception("请填写产品品牌");
            if (string.IsNullOrEmpty(request.ProductType))
                throw new Exception("请选择产品类型");
            if (string.IsNullOrEmpty(request.ProductModel))
                throw new Exception("请填写产品型号");
            if (string.IsNullOrEmpty(request.FaultDesc))
                throw new Exception("请填写故障描述");
            if (string.IsNullOrEmpty(request.DetailAddress))
                throw new Exception("请填写详细地址");

            try
            {
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
                    ProvinceId = request.ProvinceId,
                    City = request.City,
                    CityId = request.CityId,
                    District = request.District,
                    DistrictId = request.DistrictId,
                    DetailAddress = request.DetailAddress,
                    Status = 0, // 待处理
                    CreateTime = DateTime.Now,
                    IsDeleted = false
                };

                var result = await _db.Insertable(entity).ExecuteCommandAsync();
                if (result <= 0)
                {
                    throw new Exception("提交报修失败");
                }
                return entity.Id;
            }
            catch (Exception ex)
            {
                throw new Exception($"提交报修失败: {ex.Message}");
            }



        }

        /// <summary>
        /// 后台管理员更新报修状态
        /// </summary>
        public async Task UpdateStatusAsync(UpdateRepairStatusReq request, string handlerId)
        {
            var entity = await _db.Queryable<RepairOrder>()
                .FirstAsync(x => x.Id == request.Id && !x.IsDeleted);

            if (entity == null)
            {
                throw new Exception("报修单不存在");
            }

            // 校验状态值
            if (request.Status < 0 || request.Status > 3)
            {
                throw new Exception("无效的状态值");
            }
            try
            {
                entity.Status = request.Status;
                entity.Remark = request.Remark;
                entity.HandlerId = handlerId;
                entity.HandledTime = DateTime.Now;
                entity.UpdateTime = DateTime.Now;

                var result = await _db.Updateable(entity)
                    .UpdateColumns(x => new { x.Status, x.Remark, x.HandlerId, x.HandledTime, x.UpdateTime })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (result <= 0)
                {
                    throw new Exception("更新状态失败");
                }
            }
            catch(Exception ex)
            {
                throw new Exception($"更新状态失败: {ex.Message}");
            }
           
        }

        /// <summary>
        /// 查询当前用户的报修列表（小程序端）
        /// </summary>
        public async Task<TableResp<RepairOrderResp>> QueryByUserAsync(string userId, QueryRepairOrderListReq request)
        {
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
                ProvinceId = x.ProvinceId,
                City = x.City,
                CityId = x.CityId,
                District = x.District,
                DistrictId = x.DistrictId,
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
        /// 删除报修（软删除）
        /// </summary>
        public async Task DeleteAsync(string id)
        {
            var entity = await _db.Queryable<RepairOrder>()
                                  .FirstAsync(x => x.Id == id && !x.IsDeleted)
                                  .ConfigureAwait(false);

            if (entity == null)
            {
                throw new Exception("报修单不存在");
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
                    throw new Exception("删除失败");
                }

            }
            catch (Exception ex)
            {
                throw new Exception($"删除失败: {ex.Message}");
            }

        }

        /// <summary>
        /// 获取状态文本
        /// </summary>
        private string GetStatusText(int status)
        {
            return status switch
            {
                0 => "待处理",
                1 => "处理中",
                2 => "已解决",
                _ => "未知"
            };
        }
    }
}