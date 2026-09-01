using System.Collections.Generic;

namespace Infrastructure
{
    /// <summary>
    /// 表格数据响应
    /// </summary>
    public class TableResp<T> : Response
    {
        public List<T> Data { get; set; }
        public int Count { get; set; }
        public int Page { get; set; }
        public int Limit { get; set; }
    }

    public class Response
    {
        /// <summary>
        /// 操作消息【当Status不为 200时，显示详细的错误信息】
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 操作状态码，200为正常
        /// </summary>
        public int Code { get; set; }

        public Response()
        {
            Code = 200;
            Message = "操作成功";
        }
    }


    /// <summary>
    /// WEBAPI通用返回泛型基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Response<T> : Response
    {
        /// <summary>
        /// 回传的结果
        /// </summary>
        public T Data { get; set; }
    }
}
