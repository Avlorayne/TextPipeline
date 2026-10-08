using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TextPipeline.Postprocess;
using TMPro;
using UnityEngine;

namespace TextPipeline
{
    /// <summary>自带文本管线的 TMP UI 组件，不需要另外挂载 TextPipeline。</summary>
    [AddComponentMenu("UI/Text Pipeline (TMP)")]
    public class TextPipelineUGUI : TextMeshProUGUI, ITextPipeline
    {
        [SerializeField, TextArea(5, 10), Tooltip("文本母本，保留管线标签")]
        private string originalText = string.Empty;
        [SerializeField, HideInInspector] private bool hasOriginalText;
        [Tooltip("在组件开启时处理已保存的母本")] public bool processTextOnEnable = true;

        public string OriginalText => originalText;
        public TMP_Text textMesh => this;
        public Action<string> OnOriginalTextChange;

        public override string text
        {
            get => base.text;
            set
            {
                // Always execute TMP's original setter before entering the pipeline.
                base.text = value;
                SetOriginalText(value);
            }
        }

        private readonly TextPipelinePostprocessor _postprocessor = new();
        private readonly List<Coroutine> _sinkCoroutines = new();
        private ITextSource[] _textSources;
        private string _textCache;
        private bool _isProcessing;
        private bool _hasPendingText;
        private bool _pendingPreProcessing;
        private bool _pendingPostProcessing;

        private bool CanProcessText => m_isAwake && IsActive() && font != null && canvas != null;

        protected override void Awake()
        {
            base.Awake();
            EnsureOriginalText();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureOriginalText();
            if (_hasPendingText)
                ProcessPendingText();
            else if (processTextOnEnable)
                SetOriginalText(originalText);
        }

        protected override void OnDisable()
        {
            StopEffects();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            StopEffects();
            base.OnDestroy();
        }

        private void LateUpdate()
        {
            if (_hasPendingText && !_isProcessing && CanProcessText)
                ProcessPendingText();
        }

        private void EnsureOriginalText()
        {
            if (hasOriginalText) return;
            if (string.IsNullOrEmpty(originalText))
                originalText = base.text ?? string.Empty;
            hasOriginalText = true;
        }

        public void SetOriginalTextSimply(string input) => SetOriginalText(input);
        public void Refresh() => SetOriginalText(originalText);

        /// <summary>就绪时立即处理；未激活或处理重入时保留最新请求。</summary>
        public string SetOriginalText(string input, bool withPreProcessing = true, bool withPostProcessing = true)
        {
            input ??= string.Empty;
            bool changed = !hasOriginalText || originalText != input;
            originalText = input;
            hasOriginalText = true;
            _pendingPreProcessing = withPreProcessing;
            _pendingPostProcessing = withPostProcessing;
            _hasPendingText = true;

            if (!_isProcessing)
            {
                base.text = input;
                StopEffects();
                ProcessPendingText();
            }

            if (changed)
                OnOriginalTextChange?.Invoke(input);
            return _hasPendingText ? input : _textCache;
        }

        private void ProcessPendingText()
        {
            if (!_hasPendingText || _isProcessing || !CanProcessText) return;
            bool withPreProcessing = _pendingPreProcessing;
            bool withPostProcessing = _pendingPostProcessing;
            _hasPendingText = false;
            _isProcessing = true;
            try
            {
                StopEffects();
                FindSources();
                _textCache = originalText;
#if UNITY_EDITOR
                temporaryTexts.Clear();
                AddTemporaryResult("First Text", _textCache);
#endif
                if (withPreProcessing)
                {
                    foreach (var source in _textSources)
                    {
                        if (source == null) continue;
                        _textCache = source.PreProcess(_textCache) ?? string.Empty;
#if UNITY_EDITOR
                        AddTemporaryResult(source.GetType().Name, _textCache);
#endif
                    }
                }

                if (withPostProcessing)
                    _postprocessor.Process(this, _textCache, SetTextAndUpdate);
                else
                    SetTextAndUpdate(_textCache);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void SetTextAndUpdate(string displayText)
        {
            // Bypass only our override; TMP still manages input source and all dirty flags.
            base.text = displayText;
            ForceMeshUpdate();
        }

        private void FindSources()
        {
            if (_textSources != null) return;
            var sources = GetComponentsInChildren<Component>(true).OfType<ITextSource>().ToArray();
            foreach (var source in sources)
                source.TextPipeline = this;
            _textSources = sources.Length == 0
                ? Array.Empty<ITextSource>()
                : sources.OrderBy(source => TextPipelineSettings.Instance.TextSources[source.GetType()]).ToArray();
        }

        /// <summary>停止效果协程而保持文字可见；Refresh 可以重新启动效果。</summary>
        public void StopEffects()
        {
            var running = _sinkCoroutines.ToArray();
            _sinkCoroutines.Clear();
            foreach (var coroutine in running)
            {
                if (coroutine != null)
                    StopCoroutine(coroutine);
            }
        }

        void ITextPipeline.StartSinkCoroutine(Func<TextSegment[], IEnumerator> postProcess, TextSegment[] steps)
        {
            // TMP executes in edit mode too; editor previews must not start runtime effects.
            if (!Application.IsPlaying(this) || !IsActive()) return;
            _sinkCoroutines.Add(StartCoroutine(postProcess(steps)));
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            EnsureOriginalText();
        }

        [Serializable]
        private struct TemporaryResult
        {
            public string pipelineTool;
            public string result;
        }

        [SerializeField, InspectorReadOnly] private List<TemporaryResult> temporaryTexts = new();

        private void AddTemporaryResult(string pipelineTool, string result)
            => temporaryTexts.Add(new TemporaryResult { pipelineTool = pipelineTool, result = result });
#endif
    }
}
