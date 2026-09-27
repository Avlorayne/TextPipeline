using System;

namespace TextPipeline
{
    public enum MarkupDataType
    {
        String,
        Number,
        Boolean
    }

    public static class MarkupDataTypeExtensions
    {
        public static Type GetCSharpType(this MarkupDataType dataType)
        {
            return dataType switch
            {
                MarkupDataType.String => typeof(string),
                MarkupDataType.Boolean => typeof(bool),
                MarkupDataType.Number => typeof(float),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        public static bool TypeValid(this MarkupDataType dataType, Type cSharpType)
        {
            return dataType switch
            {
                MarkupDataType.String => cSharpType == typeof(string),
                MarkupDataType.Number => cSharpType == typeof(float) || cSharpType == typeof(int),
                MarkupDataType.Boolean => cSharpType == typeof(bool),
                _ => false
            };
        }

        public static bool ValueValid(this MarkupDataType dataType, object value)
        {
            return dataType switch
            {
                MarkupDataType.String => value is string,
                MarkupDataType.Number => value is int or float,
                MarkupDataType.Boolean => value is bool,
                _ => false
            };
        }

        public static object ToValue(this MarkupDataType dataType, string value)
        {
            return dataType switch
            {
                MarkupDataType.String => value,
                MarkupDataType.Number => int.TryParse(value, out var i) ? (object)i : float.Parse(value),
                MarkupDataType.Boolean => bool.TryParse(value, out var b) && b,
                _ => false
            };
        }

        public static object ToValue(this MarkupDataType dataType, string value, Type targetType)
        {
            if (!dataType.TypeValid(targetType))
                throw new ArgumentException("Target type does not match MarkupDataType.", nameof(targetType));
            if (dataType == MarkupDataType.Number)
            {
                if (targetType == typeof(int))
                    return int.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture);
                return float.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign |
                                          System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            return dataType.ToValue(value);
        }
    }
}
