using System;

namespace Infrastructure
{
    public class CommonException : Exception
    {
        private int _code;

        /// <summary>
        /// 只传消息，默认错误码为 500
        /// </summary>
        public CommonException(string message)
            : base(message)
        {
            this._code = 500;
        }

        /// <summary>
        /// 传消息和错误码
        /// </summary>
        public CommonException(string message, int code)
            : base(message)
        {
            this._code = code;
        }

        public int Code
        {
            get { return _code; }
        }
    }
}