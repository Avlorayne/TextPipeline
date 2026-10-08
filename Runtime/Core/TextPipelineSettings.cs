using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TextPipeline
{
    /// <summary>
    /// 设置pipeline的文本处理顺序按照Sequence的次序优先级执行
    /// </summary>
    public class TextPipelineSettings : ScriptableObject
    {
        public const string ResourceName = "Text TextPipeline Settings";

        private static TextPipelineSettings _instance;

        public static TextPipelineSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<TextPipelineSettings>(ResourceName);
                    if (_instance == null) _instance = CreateInstance<TextPipelineSettings>();
                    _instance.ClearCache();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            if (_instance != null) _instance.ClearCache();
            _instance = null;
        }

        // 实际序列化的是类型全名（Type 本身不可被 Unity 序列化）
        [SerializeField] private List<string> sinkSequenceTypeNames = new List<string>();
        [SerializeField] private List<string> sourceSequenceTypeNames = new List<string>();

        private Type[] SinkSequence => ParseTypes(sinkSequenceTypeNames);
        private Type[] SourceSequence => ParseTypes(sourceSequenceTypeNames);

        private static Type[] ParseTypes(List<string> names)
        {
            if (names == null || names.Count == 0) return Array.Empty<Type>();
            var result = new List<Type>(names.Count);
            foreach (var name in names)
            {
                var t = Type.GetType(name);
                if (t != null) result.Add(t);
                else Debug.LogWarning($"[TextPipelineSettings] 无法解析类型: {name}（可能已被删除或重命名）");
            }

            return result.ToArray();
        }

        private Dictionary<Type, int> _textSources;
        private Dictionary<Type, int> _textSinks;

        public Dictionary<Type, int> TextSources
        {
            get
            {
                if (_textSources == null)
                {
                    _textSources = new Dictionary<Type, int>();
                    var pair = SourceSequence.Select((value, index) => new { value, index });
                    foreach (var p in pair)
                        _textSources.Add(p.value, p.index);
                }

                return _textSources;
            }
        }

        public Dictionary<Type, int> TextSinks
        {
            get
            {
                if (_textSinks == null)
                {
                    _textSinks = new Dictionary<Type, int>();
                    var pair = SinkSequence.Select((value, index) => new { value, index });
                    foreach (var p in pair)
                        _textSinks.Add(p.value, p.index);
                }

                return _textSinks;
            }
        }

        private void ClearCache()
        {
            _textSources = null;
            _textSinks = null;
        }

        private void OnEnable() => ClearCache();

        private void OnValidate() => ClearCache();
    }
}
