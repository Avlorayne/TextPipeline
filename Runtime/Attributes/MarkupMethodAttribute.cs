using System;

namespace TextPipeline
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class MarkupMethodAttribute : Attribute
    {
        internal string Name { get; private set; }

        public MarkupMethodAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class MarkupParamAttribute : Attribute
    {
        internal string Name { get; private set; }
        internal MarkupDataType DataType { get; private set; }
        internal bool AllowDefault { get; set; }

        public MarkupParamAttribute(string name, MarkupDataType type, bool allowDefault = true)
        {
            Name = name;
            DataType = type;
            AllowDefault = allowDefault;
        }
    }
}