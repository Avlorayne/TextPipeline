using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TextPipeline.Postprocess
{
    public static class MarkupRegistry
    {
        private const string ProjectAssemblyName = "Assembly-CSharp";
        private static readonly Type InterfaceType = typeof(ITextSinkBase);
        private static readonly string RuntimeAssemblyName = InterfaceType.Assembly.GetName().Name;

        private static readonly Dictionary<string, Type> MarkupLookup = new();
        private static readonly Dictionary<Type, PropertyInfo[]> TypePropertiesLookup = new();
        private static readonly Dictionary<string,
                (string name, PropertyInfo property, MarkupDataType dataType, bool allowDefault)[]>
            DeclaredPropertiesLookup = new();

        private static readonly Dictionary<(string markup, string propertyName),
                (PropertyInfo property, MarkupDataType dataType, bool allowDefault)>
            PropertyLookup = new();

        private static readonly Dictionary<(string markup, string methodName), MethodInfo>
            MethodLookup = new();
        private static readonly Dictionary<(string markup, string methodName),
                (string name, ParameterInfo param, MarkupDataType dataType, bool allowDefault)[]>
            DeclaredParametersLookup = new();

        private static readonly Dictionary<(string markup, string methodName, string paramName),
                (ParameterInfo param, MarkupDataType dataType, bool allowDefault)>
            ParameterLookup = new();

        private static readonly Dictionary<(string markup, string methodName),
                (ParameterInfo param, string paramName, MarkupDataType dataType, bool allowDefault)>
            LowerParameterLookup = new();

        public static Type LookupForType(string markup) => MarkupLookup.GetValueOrDefault(markup);
        public static PropertyInfo[] LookupForProperties(Type type) => TypePropertiesLookup.GetValueOrDefault(type);
        public static (string name, PropertyInfo property, MarkupDataType dataType, bool allowDefault)[]
            LookupForDeclaredProperties(string markup)
            => DeclaredPropertiesLookup.GetValueOrDefault(markup) ?? Array.Empty<
                (string name, PropertyInfo property, MarkupDataType dataType, bool allowDefault)>();
        
        public static (PropertyInfo property, MarkupDataType dataType, bool allowDefault) 
            LookupForProperty(string markup, string propertyName)
            => PropertyLookup.GetValueOrDefault((markup, propertyName));

        public static MethodInfo LookupForMethod(string markup, string methodName) =>
            MethodLookup.GetValueOrDefault((markup, methodName));
        public static (string name, ParameterInfo param, MarkupDataType dataType, bool allowDefault)[]
            LookupForDeclaredParams(string markup, string methodName)
            => DeclaredParametersLookup.GetValueOrDefault((markup, methodName)) ?? Array.Empty<
                (string name, ParameterInfo param, MarkupDataType dataType, bool allowDefault)>();

        public static (ParameterInfo param, MarkupDataType dataType, bool allowDefault)
            LookupForParam(string markup, string methodName, string paramName)
            => ParameterLookup.GetValueOrDefault((markup, methodName, paramName));

        public static (ParameterInfo param, string paramName, MarkupDataType dataType, bool allowDefault)
            LookupForParam(string markup, string methodName)
            => LowerParameterLookup.GetValueOrDefault((markup, methodName));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterMarkups()
        {
            RegisterMarkups(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic &&
                                   (assembly.GetName().Name == ProjectAssemblyName ||
                                    assembly.GetName().Name == RuntimeAssemblyName)));
        }

        /// <summary>注册指定程序集内由 <see cref="MarkupAttribute"/> 标记的 <see cref="ITextSinkBase"/> 类型。</summary>
        public static void RegisterMarkups(IEnumerable<Assembly> assemblies)
        {
            MarkupLookup.Clear();
            TypePropertiesLookup.Clear();
            DeclaredPropertiesLookup.Clear();
            PropertyLookup.Clear();
            MethodLookup.Clear();
            DeclaredParametersLookup.Clear();
            ParameterLookup.Clear();
            LowerParameterLookup.Clear();

            foreach (var assembly in assemblies)
            {
                if (assembly == null || assembly.IsDynamic)
                    continue;

                foreach (Type type in GetLoadableTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                        continue;
                    if (!InterfaceType.IsAssignableFrom(type))
                        continue;
                    RegisterSink(type);
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null);
            }
        }

        private static void RegisterSink(Type type)
        {
            foreach (var typeAttribute in type.GetCustomAttributes<MarkupAttribute>())
            {
                string markup = typeAttribute.Name;
                if (!ValidName(markup) || MarkupLookup.ContainsKey(markup))
                {
                    Debug.LogError($"Invalid or duplicate markup name \"{markup}\" on {type.FullName}.");
                    continue;
                }
                MarkupLookup.Add(markup, type);

                RegisterProperties(type, markup);
                RegisterMethods(type, markup);
            }
        }

        private static void RegisterProperties(Type type, string markup)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            List<PropertyInfo> propertyInfos = new();
            List<(string name, PropertyInfo property, MarkupDataType dataType, bool allowDefault)> declared = new();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var info in properties)
            {
                var attribute = info.GetCustomAttribute<MarkupPropertyAttribute>();
                if (attribute == null) continue;
                if (!ValidName(attribute.Name) || names.Contains(attribute.Name) ||
                    info.GetIndexParameters().Length != 0 || info.GetGetMethod(true) == null ||
                    info.GetSetMethod(true) == null || !attribute.DataType.TypeValid(info.PropertyType))
                {
                    Debug.LogError($"Invalid [MarkupProperty(\"{attribute.Name}\")] on {type.FullName}.{info.Name}: " +
                                   "name must be unique and parseable, and the property must be readable, writable, " +
                                   "non-indexed, and match its MarkupDataType.");
                    continue;
                }
                names.Add(attribute.Name);
                propertyInfos.Add(info);
                declared.Add((attribute.Name, info, attribute.DataType, attribute.AllowDefault));
                PropertyLookup.Add((markup, attribute.Name), (info, attribute.DataType, attribute.AllowDefault));
            }
            TypePropertiesLookup[type] = propertyInfos.ToArray();
            DeclaredPropertiesLookup.Add(markup, declared.ToArray());
        }

        private static void RegisterMethods(Type type, string markup)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var info in methods)
            {
                var methodAttribute = info.GetCustomAttribute<MarkupMethodAttribute>();
                if (methodAttribute == null)
                    continue;
                var paramInfos = info.GetParameters();
                var declared = new List<(string name, ParameterInfo param, MarkupDataType dataType, bool allowDefault)>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                bool valid = ValidName(methodAttribute.Name) && !MethodLookup.ContainsKey((markup, methodAttribute.Name)) &&
                             !info.ContainsGenericParameters;
                foreach (var param in paramInfos)
                {
                    var attribute = param.GetCustomAttribute<MarkupParamAttribute>();
                    if (attribute == null)
                        continue;
                    if (!ValidName(attribute.Name) || !names.Add(attribute.Name) ||
                        param.IsOut || param.ParameterType.IsByRef ||
                        !attribute.DataType.TypeValid(param.ParameterType))
                        valid = false;
                    declared.Add((attribute.Name, param, attribute.DataType, attribute.AllowDefault));
                }
                if (!valid)
                {
                    Debug.LogError($"Invalid [MarkupMethod(\"{methodAttribute.Name}\")] on {type.FullName}.{info.Name}: " +
                                   "method and parameter names must be unique and parseable; annotated parameters " +
                                   "must match their MarkupDataType and cannot be ref/out.");
                    continue;
                }
                MethodLookup.Add((markup, methodAttribute.Name), info);
                DeclaredParametersLookup.Add((markup, methodAttribute.Name), declared.ToArray());

                // 在 markup, methodAttribute.Name 路径只有唯一解时，才能进这个分支，以维护一个低级表，满足语法糖
                if (paramInfos.Length == 1 && declared.Count == 1)
                {
                    var parameter = declared[0];
                    LowerParameterLookup.Add((markup, methodAttribute.Name),
                        (parameter.param, parameter.name, parameter.dataType, parameter.allowDefault));
                }

                foreach (var parameter in declared)
                {
                    ParameterLookup.Add((markup, methodAttribute.Name, parameter.name),
                        (parameter.param, parameter.dataType, parameter.allowDefault));
                }
            }
        }

        private static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] < 'a' || name[0] > 'z') return false;
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-') continue;
                return false;
            }
            return true;
        }
    }
}
