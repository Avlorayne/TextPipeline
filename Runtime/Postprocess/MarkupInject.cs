using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TextPipeline.Postprocess
{
    public static class MarkupInject
    {
        public static void PropertiesInject(ITextSinkBase sink,
            (PropertyInfo property, MarkupDataType dataType, bool allowDefault, string value)[] items)
        {
            foreach (var item in items)
            {
                if(!item.property.CanWrite) continue;
                if(item.property.GetIndexParameters().Length > 0) continue;
                item.property.SetValue(sink, item.dataType.ToValue(item.value, item.property.PropertyType));
            }
        }

        public static Dictionary<PropertyInfo, object> GetSinkSnapshot(ITextSinkBase sink)
        {
            var paramInfos = MarkupRegistry.LookupForProperties(sink.GetType());
            Dictionary<PropertyInfo, object> snapshot = new();
            foreach (var paramInfo in paramInfos)
            {
                var value = paramInfo.GetValue(sink);
                snapshot.Add(paramInfo, value);
            }
            return snapshot;
        }

        public static void InjectBySnapShot(ITextSinkBase sink, IReadOnlyDictionary<PropertyInfo, object> snapshot)
        {
            foreach (var pair in snapshot)
            {
                var propertyInfo = pair.Key;
                var value = pair.Value;
                propertyInfo.SetValue(sink, value);
            }
        }

        public static object MethodInjectWithInvoke(ITextSinkBase sink, MethodInfo methodInfo,
            object[] paramItems)
        {
            return methodInfo.Invoke(sink, paramItems);
        }

        public static object[] ResolveMethodArguments(MethodInfo methodInfo,
            (ParameterInfo param, MarkupDataType dataType, bool allowDefault, string value)[] paramItems)
        {
            var paramList = new List<KeyValuePair<ParameterInfo, object>>();
            var paramInfos = methodInfo.GetParameters();
            // 全部先设为 default 值
            foreach (var paramInfo in paramInfos)
            {
                paramList.Add(paramInfo.HasDefaultValue
                    ? new KeyValuePair<ParameterInfo, object>(paramInfo, paramInfo.DefaultValue)
                    : new KeyValuePair<ParameterInfo, object>(paramInfo,
                        paramInfo.ParameterType.IsValueType ? System.Activator.CreateInstance(paramInfo.ParameterType) : null));
            }
            // 再覆盖输入的值
            foreach (var item in paramItems)
            {
                for (int i = 0; i < paramList.Count; i++)
                {
                    if(paramList[i].Key !=  item.param) continue;
                    paramList[i] = new KeyValuePair<ParameterInfo, object>(item.param,
                        item.dataType.ToValue(item.value, item.param.ParameterType));
                }
            }
            // 最后按param原顺序筛选出参数注入
            var valueList = new List<object>();
            foreach (var pair in paramList)
            {
                valueList.Add(pair.Value);
            }
            
            return valueList.ToArray();
        }
    }
}
