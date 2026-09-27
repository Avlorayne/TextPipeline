using System;

namespace TextPipeline
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class MarkupMethodAttribute : Attribute
    {
        public string Name { get; private set; }

        public MarkupMethodAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public class MarkupParamAttribute : Attribute
    {
        public string Name { get; private set; }
        public MarkupDataType DataType { get; private set; }
        public bool AllowDefault { get; set; }

        public MarkupParamAttribute(string name, MarkupDataType type, bool allowDefault = true)
        {
            Name = name;
            DataType = type;
            AllowDefault = allowDefault;
        }
    }
}