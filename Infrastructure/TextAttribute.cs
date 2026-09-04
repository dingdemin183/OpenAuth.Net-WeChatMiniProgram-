// Infrastructure/TextAttribute.cs

using System;

namespace Infrastructure
{
    /// <summary>
    /// 文本描述特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TextAttribute : Attribute
    {
        public string Value { get; }

        public TextAttribute(string value = "")
        {
            Value = value;
        }
    }
}