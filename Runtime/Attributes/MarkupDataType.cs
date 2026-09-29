using System;

namespace TextPipeline
{
    public enum MarkupDataType
    {
        String,
        Float,
        Int,
        Boolean
    }

    internal static class MarkupDataTypeExtensions
    {
        internal static Type GetCSharpType(this MarkupDataType dataType)
        {
            return dataType switch
            {
                MarkupDataType.String => typeof(string),
                MarkupDataType.Boolean => typeof(bool),
                MarkupDataType.Float => typeof(float),
                MarkupDataType.Int => typeof(int),
                _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
            };
        }

        internal static bool TypeValid(this MarkupDataType dataType, Type cSharpType)
        {
            return dataType switch
            {
                MarkupDataType.String => cSharpType == typeof(string),
                MarkupDataType.Float => cSharpType == typeof(float),
                MarkupDataType.Int => cSharpType == typeof(int),
                MarkupDataType.Boolean => cSharpType == typeof(bool),
                _ => false
            };
        }

        internal static bool ValueValid(this MarkupDataType dataType, object value)
        {
            return dataType switch
            {
                MarkupDataType.String => value is string,
                MarkupDataType.Float => value is float,
                MarkupDataType.Int => value is int,
                MarkupDataType.Boolean => value is bool,
                _ => false
            };
        }

        internal static object ToValue(this MarkupDataType dataType, string value)
        {
            return dataType switch
            {
                MarkupDataType.String => value,
                MarkupDataType.Float => float.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign |
                    System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture),
                MarkupDataType.Int => int.Parse(value, System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture),
                MarkupDataType.Boolean => bool.TryParse(value, out var b) && b,
                _ => false
            };
        }

        internal static object ToValue(this MarkupDataType dataType, string value, Type targetType)
        {
            if (!dataType.TypeValid(targetType))
                throw new ArgumentException("Target type does not match MarkupDataType.", nameof(targetType));
            if (dataType == MarkupDataType.Float)
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
