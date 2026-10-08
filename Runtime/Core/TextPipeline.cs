using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TextPipeline.Postprocess;
using TMPro;
using UnityEngine;

namespace TextPipeline
{
    public class TextPipeline : MonoBehaviour, ITextPipeline
    {
        private static TextPipelineSettings Settings => TextPipelineSettings.Instance;

        [SerializeField, Tooltip("文本母本，Editor 保存时与 TMP 中 Text 内容同步")]
        private string originalText = string.Empty;

        public string OriginalText
        {
            get => originalText;
            private set
            {
                SetOriginalText(value);
                OnOriginalTextChange?.Invoke(value);
            }
        }

        [Tooltip("在组件开启时处理文本")] public bool processTextOnEnable = true;

        public TMP_Text textMesh;
        TMP_Text ITextPipeline.textMesh => textMesh;
        private ITextSource[] _textSources;
        private readonly TextPipelinePostprocessor _postprocessor = new();
        private readonly List<Coroutine> _sinkCoroutines = new();

        private string _textCache;
        public Action<string> OnOriginalTextChange;


        void Awake()
        {
            FindAllComponents();
        }

        void OnEnable()
        {
            FindAllComponents();
#if UNITY_EDITOR
            temporaryTexts.Clear();
#endif
            if (processTextOnEnable)
                SetOriginalText(originalText, withPreProcessing: true, withPostProcessing: true);
        }

        void OnDisable()
        {
            CancelSinkRuns();
        }

        void Start()
        {
        }

        /// <summary>
        /// 外部请求注入，设置后会立即开始文本处理并显示
        /// </summary>
        /// <param name="text">注入文本</param>
        public void SetOriginalTextSimply(string text)
        {
            SetOriginalText(text);
        }

        /// <summary>
        /// 外部请求刷新处理，文本从TMP获取
        /// </summary>
        public void Refresh()
        {
            SetOriginalText(originalText);
        }

        /// <summary>
        /// 外部请求注入文本，设置后会开始文本处理
        /// </summary>
        /// <param name="text">注入文本</param>
        /// <param name="withPreProcessing">是否在设置后对文本进行预处理</param>
        /// <param name="withPostProcessing">是否在设置后对文本进行后处理</param>
        public string SetOriginalText(
            string text,
            bool withPreProcessing = true,
            bool withPostProcessing = true)
        {
            CancelSinkRuns();
            if (_textSources == null || textMesh == null)
                FindAllComponents();

            originalText = text;
            _textCache = originalText;

#if UNITY_EDITOR
            AddTemporaryResult("First Text", _textCache);
#endif

            if (withPreProcessing && _textSources != null)
                Preprocess(text);

            if (withPostProcessing)
                Postprocess(_textCache);
            else
                SetTextAndUpdate(_textCache);

            return _textCache;
        }

        private void Preprocess(string text)
        {
            foreach (var source in _textSources)
            {
                if (source == null) continue;
                _textCache = source.PreProcess(_textCache);
#if UNITY_EDITOR
                AddTemporaryResult(source.GetType().Name, _textCache);
#endif
            }
        }

        private void Postprocess(string authoringText)
        {
            _postprocessor.Process(this, authoringText, SetTextAndUpdate);
        }

        private void SetTextAndUpdate(string text)
        {
            textMesh.text = text;
            textMesh.ForceMeshUpdate();
        }
        
        internal void StartSinkCoroutine(Func<TextSegment[], IEnumerator> postProcess, TextSegment[] steps)
        {
            var coroutine = StartCoroutine(postProcess(steps));
            _sinkCoroutines.Add(coroutine);
        }

        void ITextPipeline.StartSinkCoroutine(Func<TextSegment[], IEnumerator> postProcess, TextSegment[] steps)
            => StartSinkCoroutine(postProcess, steps);
        
        private void CancelSinkRuns()
        {
            var running = _sinkCoroutines.ToArray();
            _sinkCoroutines.Clear();
            foreach (var coroutine in running)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
        }

        private void FindAllComponents()
        {
            textMesh ??= GetComponentInChildren<TMP_Text>();

            if (_textSources != null) return;
            var components = GetComponentsInChildren<Component>(true);

            var sources = components.OfType<ITextSource>().ToArray();
            foreach (var source in sources) source.TextPipeline = this;

            _textSources = SortSources(sources);
        }

        private ITextSource[] SortSources(ITextSource[] sources)
        {
            if (sources == null || sources.Length == 0) return Array.Empty<ITextSource>();
            return sources.OrderBy(t => Settings.TextSources[t.GetType()]).ToArray();
        }

        private ITextSinkBase[] SortSinks(ITextSinkBase[] sinks)
        {
            return TextPipelinePostprocessor.SortSinks(sinks);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            textMesh ??= GetComponentInChildren<TMP_Text>();
            if (textMesh != null && string.IsNullOrEmpty(originalText))
                originalText = textMesh.text;
        }

        [Serializable]
        struct TemporaryResult
        {
            public string pipelineTool;
            public string result;
        }

        [SerializeField, InspectorReadOnly] List<TemporaryResult> temporaryTexts = new();

        void AddTemporaryResult(string pipelineTool, string result)
            => temporaryTexts.Add(new TemporaryResult { pipelineTool = pipelineTool, result = result });
#endif
    }
}
