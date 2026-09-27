using System;

namespace TextPipeline
{
    /// <summary>
    /// 标记 ITextSinkBase 实现类型上的属性/字段，使其成为 <c>&lt;tag : propertyName = value&gt;</c>
    /// 中名为 <paramref>
    ///     <name>propertyName</name>
    /// </paramref>
    /// 的合法接收目标。
    /// 未被任何 Sink 以同名特性声明的属性名会在解析阶段被拒绝。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class MarkupPropertyAttribute : Attribute
    {
        public string Name { get; }
        public MarkupDataType DataType { get; }
        public bool AllowDefault { get; }

        public MarkupPropertyAttribute(string name, MarkupDataType dataType,
            bool allowDefault = true)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("标记属性名不能为空。", nameof(name));
            Name = name;
            DataType = dataType;
            AllowDefault = allowDefault;
        }
    }
}
