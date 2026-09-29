using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections;
using UnityEngine;

namespace TextPipeline.Postprocess
{
    public static class MarkupRegistry
    {
        private const string ProjectAssemblyName = "Assembly-CSharp";
        private static readonly Type InterfaceType = typeof(ITextSinkBase);
        private static readonly string RuntimeAssemblyName = InterfaceType.Assembly.GetName().Name;

        private static readonly Dictionary<string, Type> MarkupLookup = new();
        private static readonly Dictionary<Type, string[]> PropertiesLookup = new();
        private static readonly Dictionary<(Type type, string method), RegisteredParameter[]> ParameterLookup = new();

        private static readonly Dictionary<(Type type, string property), MarkupPropertyAttribute>
            PropertyLookup = new();

        private static readonly Dictionary<(Type type, string property), Func<ITextSinkBase, float>> FloatGetterLookup =
            new();

        private static readonly Dictionary<(Type type, string property), Func<ITextSinkBase, int>> INTGetterLookup =
            new();

        private static readonly Dictionary<(Type type, string property), Func<ITextSinkBase, string>>
            StringGetterLookup = new();

        private static readonly Dictionary<(Type type, string property), Func<ITextSinkBase, bool>> BoolGetterLookup =
            new();

        private static readonly Dictionary<(Type type, string property), Action<ITextSinkBase, string>>
            StringSetterLookup = new();

        private static readonly Dictionary<(Type type, string property), Action<ITextSinkBase, float>>
            FloatSetterLookup = new();

        private static readonly Dictionary<(Type type, string property), Action<ITextSinkBase, int>> IntSetterLookup =
            new();

        private static readonly Dictionary<(Type type, string property), Action<ITextSinkBase, bool>> BoolSetterLookup =
            new();

        private static readonly Dictionary<(Type type, string method), Func<ITextSinkBase, object[], object>>
            MethodLookup = new();

        private sealed class RegisteredParameter
        {
            public string Name;
            public MarkupParamAttribute Attribute;
            public object DefaultValue;
        }

        public static object Get(ITextSinkBase instance, string property)
        {
            var type = instance.GetType();
            if (FloatGetterLookup.TryGetValue((type, property), out var floatGetter))
                return floatGetter(instance);
            if (INTGetterLookup.TryGetValue((type, property), out var intGetter))
                return intGetter(instance);
            if (StringGetterLookup.TryGetValue((type, property), out var stringGetter))
                return stringGetter(instance);
            if (BoolGetterLookup.TryGetValue((type, property), out var boolGetter))
                return boolGetter(instance);
            Debug.LogError($"Couldn't find property {type}.{property}");
            return null;
        }

        public static void Set(ITextSinkBase instance, string property, object value)
        {
            var type = instance.GetType();
            if (FloatSetterLookup.TryGetValue((type, property), out var floatSetter))
            {
                if (value is float floatValue)
                    floatSetter(instance, floatValue);
                else
                    Debug.LogError($"{type}.{property}'s type is float, while value's type is {value?.GetType()}");
            }
            else if (IntSetterLookup.TryGetValue((type, property), out var intSetter))
            {
                if (value is int intValue)
                    intSetter(instance, intValue);
                else
                    Debug.LogError($"{type}.{property}'s type is int, while value's type is {value?.GetType()}");
            }
            else if (StringSetterLookup.TryGetValue((type, property), out var stringSetter))
            {
                if (value == null || value is string)
                    stringSetter(instance, (string)value);
                else
                    Debug.LogError($"{type}.{property}'s type is string, while value's type is {value.GetType()}");
            }
            else if (BoolSetterLookup.TryGetValue((type, property), out var boolSetter))
            {
                if (value is bool boolValue)
                    boolSetter(instance, boolValue);
                else
                    Debug.LogError($"{type}.{property}'s type is bool, while value's type is {value?.GetType()}");
            }
            else
                Debug.LogError($"No Setter Found By {type}.{property}!");
        }

        public static object Invoke(ITextSinkBase instance, string method, object[] arguments)
        {
            var path = (instance.GetType(), method);
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (arguments.Length != ParameterLookup[path].Length)
                throw new ArgumentException($"Incorrect argument count for {path.Item1}.{method}.", nameof(arguments));
            return MethodLookup[path](instance, arguments);
        }

        public static object Invoke(ITextSinkBase instance, string method, KeyValuePair<string, object>[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            var type = instance.GetType();
            var arguments = ResolveMethodArguments((type, method), args);
            return MethodLookup[(type, method)](instance, arguments);
        }

        private static object[] ResolveMethodArguments((Type type, string method) path,
            KeyValuePair<string, object>[] args)
        {
            var parameters = ParameterLookup[path];
            object[] values = parameters.Select(parameter => parameter.DefaultValue).ToArray();

            foreach (var arg in args)
            {
                bool found = false;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].Attribute == null || arg.Key != parameters[i].Name) continue;
                    values[i] = arg.Value;
                    found = true;
                    break;
                }

                if (!found)
                    throw new ArgumentException($"Unknown argument {arg.Key} for {path.type}.{path.method}.",
                        nameof(args));
            }

            return values;
        }

        public static Type LookupForType(string typeName) => MarkupLookup.GetValueOrDefault(typeName);

        public static bool HasMethod(string markup, string method)
        {
            return LookupForType(markup) is { } type && MethodLookup.ContainsKey((type, method));
        }

        public static bool TryGetProperty(string markup, string property, out MarkupDataType dataType)
        {
            if (LookupForType(markup) is { } type && PropertyLookup.TryGetValue((type, property), out var info))
            {
                dataType = info.DataType;
                return true;
            }

            dataType = default;
            return false;
        }

        public static bool TryGetParameter(string markup, string method, string name, out MarkupDataType dataType)
        {
            if (LookupForType(markup) is { } type && ParameterLookup.TryGetValue((type, method), out var parameters))
                foreach (var parameter in parameters)
                    if (parameter.Attribute != null && parameter.Name == name)
                    {
                        dataType = parameter.Attribute.DataType;
                        return true;
                    }

            dataType = default;
            return false;
        }

        public static string GetSingleParameterName(string markup, string method)
        {
            if (LookupForType(markup) is not { } type ||
                !ParameterLookup.TryGetValue((type, method), out var parameters))
                return null;
            return parameters.Length == 1 && parameters[0].Attribute != null ? parameters[0].Name : null;
        }

        public static (string name, bool allowDefault)[] LookupForDeclaredProperties(string markup)
        {
            var type = LookupForType(markup);
            return PropertiesLookup[type].Select(name => (name, PropertyLookup[(type, name)].AllowDefault)).ToArray();
        }

        public static (string name, bool allowDefault)[] LookupForDeclaredParams(string markup, string method)
        {
            return ParameterLookup[(LookupForType(markup), method)]
                .Where(parameter => parameter.Attribute != null)
                .Select(parameter => (parameter.Name, parameter.Attribute.AllowDefault)).ToArray();
        }

        public static string[] LookupForProperties(Type type)
        {
            return PropertiesLookup[type];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterMarkups()
        {
            RegisterMarkups(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic &&
                                   (assembly.GetName().Name == ProjectAssemblyName ||
                                    assembly.GetName().Name == RuntimeAssemblyName)));
        }

        public static void RegisterMarkups(IEnumerable<Assembly> assemblies)
        {
            MarkupLookup.Clear();
            FloatGetterLookup.Clear();
            FloatSetterLookup.Clear();
            INTGetterLookup.Clear();
            IntSetterLookup.Clear();
            StringGetterLookup.Clear();
            StringSetterLookup.Clear();
            BoolGetterLookup.Clear();
            BoolSetterLookup.Clear();
            MethodLookup.Clear();
            ParameterLookup.Clear();
            PropertiesLookup.Clear();
            PropertyLookup.Clear();

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
                    RegisterMarkup(type);
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

        private static void RegisterMarkup(Type type)
        {
            if (PropertiesLookup.ContainsKey(type)) return;
            var attributes = type.GetCustomAttributes<MarkupAttribute>().ToArray();
            if (attributes.Length == 0) return;
            RegisterProperties(type);
            RegisterMethods(type);
            foreach (var attribute in attributes)
            {
                if (!ValidName(attribute.Name) || !MarkupLookup.TryAdd(attribute.Name, type))
                    Debug.LogError($"Invalid or duplicate markup name \"{attribute.Name}\" on {type.FullName}.");
            }
        }

        private static void RegisterMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var methodAttribute = method.GetCustomAttribute<MarkupMethodAttribute>();
                if (methodAttribute == null) continue;
                var paramInfos = method.GetParameters();
                if (!ValidName(methodAttribute.Name) || MethodLookup.ContainsKey((type, methodAttribute.Name)) ||
                    method.ContainsGenericParameters ||
                    (method.ReturnType != typeof(void) && !typeof(IEnumerator).IsAssignableFrom(method.ReturnType)))
                {
                    Debug.LogError(
                        $"Invalid [MarkupMethod(\"{methodAttribute.Name}\")] on {type.FullName}.{method.Name}.");
                    continue;
                }

                if (!RegisterParams((type, methodAttribute.Name), paramInfos)) continue;
                GenerateMethods(method, (type, methodAttribute.Name));
            }
        }

        private static bool RegisterParams((Type type, string method) path, ParameterInfo[] parameters)
        {
            var registered = new RegisteredParameter[parameters.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var attribute = param.GetCustomAttribute<MarkupParamAttribute>();
                if (param.IsOut || param.ParameterType.IsByRef ||
                    (attribute != null && (!ValidName(attribute.Name) || !names.Add(attribute.Name) ||
                                           !attribute.DataType.TypeValid(param.ParameterType))))
                {
                    Debug.LogError($"Invalid parameter {param.Name} on {path.type}.{path.method}.");
                    return false;
                }

                registered[i] = new RegisteredParameter
                {
                    Name = attribute?.Name ?? param.Name,
                    Attribute = attribute,
                    DefaultValue = param.HasDefaultValue
                        ? param.DefaultValue
                        : param.ParameterType.IsValueType
                            ? Expression.Lambda<Func<object>>(
                                Expression.Convert(Expression.Default(param.ParameterType), typeof(object))).Compile()()
                            : null
                };
            }

            ParameterLookup.Add(path, registered);
            return true;
        }

        private static void GenerateMethods(MethodInfo method, (Type type, string method) path)
        {
            var sink = Expression.Parameter(typeof(ITextSinkBase), "sink");
            var arguments = Expression.Parameter(typeof(object[]), "arguments");
            var parameters = method.GetParameters()
                .Select((parameter, index) => (Expression)Expression.Convert(
                    Expression.ArrayIndex(arguments, Expression.Constant(index)), parameter.ParameterType));
            var call = Expression.Call(Expression.Convert(sink, method.DeclaringType), method, parameters);
            Expression body = method.ReturnType == typeof(void)
                ? Expression.Block(call, Expression.Constant(null, typeof(object)))
                : Expression.Convert(call, typeof(object));
            MethodLookup.Add(path,
                Expression.Lambda<Func<ITextSinkBase, object[], object>>(body, sink, arguments).Compile());
        }

        private static void RegisterProperties(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var propertyList = new List<string>();

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<MarkupPropertyAttribute>();
                if (attribute == null) continue;
                if (!ValidName(attribute.Name) || PropertyLookup.ContainsKey((type, attribute.Name)) ||
                    property.GetIndexParameters().Length != 0 || property.GetGetMethod(true) == null ||
                    property.GetSetMethod(true) == null || !attribute.DataType.TypeValid(property.PropertyType))
                {
                    Debug.LogError(
                        $"Invalid [MarkupProperty(\"{attribute.Name}\")] on {type.FullName}.{property.Name}: " +
                        "name must be unique and parseable, and the property must be readable, writable, " +
                        "non-indexed, and match its MarkupDataType.");
                    continue;
                }

                GenerateGetter(property, (type, attribute.Name), attribute.DataType);
                GenerateSetter(property, (type, attribute.Name), attribute.DataType);
                PropertyLookup.Add((type, attribute.Name), attribute);
                propertyList.Add(attribute.Name);
            }

            PropertiesLookup.Add(type, propertyList.ToArray());
        }

        private static void GenerateGetter(PropertyInfo property, (Type type, string property) path,
            MarkupDataType dataType)
        {
            var param = Expression.Parameter(typeof(ITextSinkBase), "sink");
            var body = Expression.Property(Expression.Convert(param, property.DeclaringType), property);
            UnaryExpression convert;
            switch (dataType)
            {
                case MarkupDataType.String:
                    convert = Expression.Convert(body, typeof(string));
                    var getterString = Expression.Lambda<Func<ITextSinkBase, string>>(convert, param).Compile();
                    StringGetterLookup.Add(path, getterString);
                    break;
                case MarkupDataType.Float:
                    convert = Expression.Convert(body, typeof(float));
                    var getterFloat = Expression.Lambda<Func<ITextSinkBase, float>>(convert, param).Compile();
                    FloatGetterLookup.Add(path, getterFloat);
                    break;
                case MarkupDataType.Int:
                    convert = Expression.Convert(body, typeof(int));
                    var getterInt = Expression.Lambda<Func<ITextSinkBase, int>>(convert, param).Compile();
                    INTGetterLookup.Add(path, getterInt);
                    break;
                case MarkupDataType.Boolean:
                    convert = Expression.Convert(body, typeof(bool));
                    var getterBool = Expression.Lambda<Func<ITextSinkBase, bool>>(convert, param).Compile();
                    BoolGetterLookup.Add(path, getterBool);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void GenerateSetter(PropertyInfo property, (Type type, string property) path,
            MarkupDataType dataType)
        {
            var instance = Expression.Parameter(typeof(ITextSinkBase), "sink");
            var left = Expression.Property(Expression.Convert(instance, property.DeclaringType), property);
            ParameterExpression value;
            switch (dataType)
            {
                case MarkupDataType.String:
                    value = Expression.Parameter(typeof(string), "value");
                    var assignString = Expression.Assign(left, value);
                    var setterString = Expression
                        .Lambda<Action<ITextSinkBase, string>>(Expression.Block(assignString, Expression.Empty()),
                            instance, value).Compile();
                    StringSetterLookup.Add(path, setterString);
                    break;
                case MarkupDataType.Float:
                    value = Expression.Parameter(typeof(float), "value");
                    var assignFloat = Expression.Assign(left, value);
                    var setterFloat = Expression
                        .Lambda<Action<ITextSinkBase, float>>(Expression.Block(assignFloat, Expression.Empty()),
                            instance, value).Compile();
                    FloatSetterLookup.Add(path, setterFloat);
                    break;
                case MarkupDataType.Int:
                    value = Expression.Parameter(typeof(int), "value");
                    var assignInt = Expression.Assign(left, value);
                    var setterInt = Expression
                        .Lambda<Action<ITextSinkBase, int>>(Expression.Block(assignInt, Expression.Empty()), instance,
                            value).Compile();
                    IntSetterLookup.Add(path, setterInt);
                    break;
                case MarkupDataType.Boolean:
                    value = Expression.Parameter(typeof(bool), "value");
                    var assignBool = Expression.Assign(left, value);
                    var setterBool = Expression
                        .Lambda<Action<ITextSinkBase, bool>>(Expression.Block(assignBool, Expression.Empty()), instance,
                            value).Compile();
                    BoolSetterLookup.Add(path, setterBool);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name[0] < 'a' || name[0] > 'z') return false;
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') continue;
                return false;
            }

            return true;
        }
    }
}