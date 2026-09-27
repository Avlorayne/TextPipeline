using System;

namespace TextPipeline
{
    /// <summary>
    /// 为 <see cref="ITextSinkBase"/> 声明其在文本语法中的标签名。
    /// 例如 <c>[Markup("wave")]</c> 对应 <c>&lt;wave&gt;...&lt;/wave&gt;</c>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class MarkupAttribute : Attribute
    {
        public string Name { get; }

        public MarkupAttribute(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("标记名不能为空。", nameof(name));
            Name = name;
        }
    }
}
