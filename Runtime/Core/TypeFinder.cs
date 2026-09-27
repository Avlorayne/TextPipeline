using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TextPipeline
{
    public static class TypeFinder
    {
        private static readonly ConcurrentDictionary<string, List<Type>> _cache = new();

        public static IReadOnlyList<Type> FindDerivedTypes<TBase>(
            string targetNamespace = null,
            bool includeSubNamespaces = false,
            params Assembly[] assemblies)
        {
            var baseType = typeof(TBase);
            var cacheKey = $"{baseType.FullName}_{targetNamespace}_{includeSubNamespaces}";

            // 从缓存获取
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            var targetAssemblies = assemblies?.Any() == true
                ? assemblies
                : AppDomain.CurrentDomain.GetAssemblies();

            var result = new List<Type>();

            foreach (var asm in targetAssemblies)
            {
                try
                {
                    var types = asm.GetTypes()
                        .Where(t => t.IsClass &&
                                    !t.IsAbstract &&
                                    !t.IsGenericTypeDefinition &&
                                    baseType.IsAssignableFrom(t));

                    if (!string.IsNullOrEmpty(targetNamespace))
                    {
                        types = includeSubNamespaces
                            ? types.Where(t => t.Namespace?.StartsWith(targetNamespace) == true)
                            : types.Where(t => t.Namespace == targetNamespace);
                    }

                    result.AddRange(types);
                }
                catch
                {
                    continue;
                }
            }

            result = result.Distinct().ToList();
            _cache[cacheKey] = result;
            return result;
        }

        // 清除缓存（当动态加载新程序集时）
        public static void ClearCache() => _cache.Clear();
    }
}