// Infrastructure/EnumExtensions.cs

using System;
using System.Reflection;

namespace Infrastructure
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 获取枚举的 Text 特性值
        /// </summary>
        public static string GetText(this Enum enumValue)
        {
            var field = enumValue.GetType().GetField(enumValue.ToString());
            var attribute = field?.GetCustomAttribute<TextAttribute>();
            return attribute?.Value ?? enumValue.ToString();
        }
    }
}