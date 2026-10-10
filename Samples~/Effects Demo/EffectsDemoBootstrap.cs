using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PipelineComponent = TextPipeline.TextPipeline;

namespace TextPipeline.Samples
{
    /// <summary>Builds visual effects and pipeline behavior examples when the demo scene starts.</summary>
    public sealed class EffectsDemoBootstrap : MonoBehaviour
    {
        private const int LifetimePipelineIndex = 3;

        [Tooltip("预处理示例中 {{player}} 的替换值。")]
        public string playerName = "Ada";

        [TextArea(2, 4), Tooltip("先替换变量，再把 [[wave]] 简写展开为管线标签，最后执行 wave 效果。")]
        public string preprocessing = "Hello, {{player}}! {{effect}}";

        [TextArea(2, 4)] public string typer =
            "<typer : speed = 16>Hello, <typer : pause(0.4)>welcome to Text Pipeline.</typer>";

        [TextArea(2, 4)] public string wave =
            "<wave : amplitude = 9, frequency = 0.45, speed = 4>Every letter rides a wave.</wave>";

        [TextArea(2, 4), Tooltip("外层 soft 的 intensity=0.5；内层 HARD SHAKE 覆盖为 14；退出内层后右侧 soft 恢复 0.5。")]
        public string shake =
            "<shake : intensity = 0.5>soft " +
            "<shake : intensity = 14><color=#FF9F43><b>HARD SHAKE</b></color></shake>" +
            " soft</shake>";

        [TextArea(2, 4)] public string rainbow =
            "<rainbow : speed = 0.18, spread = 0.08>Color travels through the text.</rainbow>";

        [TextArea(3, 6)] public string audit =
            "<audit : label = \"outer\", strength = 0.25, count = 2>outer " +
            "<audit : label = \"inner\", strength = 1><b>inner</b></audit> outer" +
            "<audit : mark(\"checkpoint\")></audit>  " +
            "<audit : label = \"scope one\" | scope = 1>scope 1</audit>  " +
            "<audit : label = \"sibling\", enabled = false | scope = 0>sibling</audit>" +
            "<audit : mark>";

        [TextArea(2, 4), Tooltip("运行数秒后点击 Replay Scope：先停止约 1.2 秒；scope 0 保留 Sink 编号和累计 age，scope 1 新建 Sink 并从 age=0 开始。")]
        public string lifetime =
            "<lifetime : label = \"scope 0\", speed = 0.6 | scope = 0>KEEP AGE</lifetime>  " +
            "<lifetime : label = \"scope 1\", speed = 0.6 | scope = 1>RESET AGE</lifetime>";

        [TextArea(2, 4)] public string serial =
            "<audit : label = \"serial wrapper\", strength = 0.4>" +
            "<serial : label = \"A\", frames = 30>ALPHA</serial>   " +
            "<serial : label = \"B\", frames = 30>BETA</serial></audit>";

        [TextArea(2, 4)] public string parallel =
            "<audit : label = \"parallel wrapper\", strength = 0.4>" +
            "<parallel : label = \"A\", frames = 30>ALPHA</parallel>   " +
            "<parallel : label = \"B\", frames = 30>BETA</parallel></audit>";

        private readonly List<ITextPipeline> _pipelines = new();
        private readonly List<TMP_Text> _preprocessingInputs = new();
        private TMP_Text _traceText;
        private ScrollRect _traceScroll;
        private TMP_Text _lifetimeStatusText;
        private Coroutine _scopeReplayCoroutine;

        private void Update()
        {
            if (_lifetimeStatusText == null) return;
            var summary = EffectsDemoLifetime.Summary;
            if (_lifetimeStatusText.text != summary)
                _lifetimeStatusText.text = summary;
        }

        private void OnDestroy()
        {
            EffectsDemoTrace.Changed -= UpdateTrace;
        }

        private IEnumerator Start()
        {
            EffectsDemoTrace.Clear();
            EffectsDemoLifetime.Clear();
            EffectsDemoTrace.Changed += UpdateTrace;
            var canvasObject = new GameObject("Effects Demo Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("Effects Demo Event System", typeof(EventSystem), typeof(StandaloneInputModule));

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.055f, 0.075f, 0.13f);

            // Let the overlay canvas establish its rect before vertex effects snapshot TMP meshes.
            yield return null;

            AddText(canvasObject.transform, "Title", "TEXT PIPELINE  /  EFFECTS DEMO", -16, 30, false, 42);
            AddText(canvasObject.transform, "Hint",
                "Scroll down for PREPROCESS: variables -> markup -> effects. Replay Scope compares sink lifetimes.",
                -66, 18, false, 44);
            AddButton(canvasObject.transform, "Replay Scope", -384, ReplayScope);
            AddButton(canvasObject.transform, "Replay", -222, Replay);
            AddButton(canvasObject.transform, "Refresh", -60, Refresh);

            var scrollObject = new GameObject("Examples Scroll View", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(canvasObject.transform, false);
            scrollObject.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.03f, 0.05f);
            scrollRect.anchorMax = new Vector2(0.63f, 0.84f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 1580);

            var scrolling = scrollObject.GetComponent<ScrollRect>();
            scrolling.viewport = viewportRect;
            scrolling.content = contentRect;
            scrolling.horizontal = false;
            scrolling.vertical = true;
            scrolling.movementType = ScrollRect.MovementType.Clamped;
            scrolling.scrollSensitivity = 30;

            // The scroll viewport needs a layout pass before effects capture TMP mesh data.
            yield return null;

            AddRow(content.transform, "TYPER  /  serial segments and coroutine marker", typer, -12, 65, true);
            AddRow(content.transform, "WAVE  /  vertex animation", wave, -130, 65, true);
            AddRow(content.transform, "SHAKE  /  nested: soft 0.5, HARD 14, soft 0.5", shake, -248, 65);
            AddRow(content.transform, "LIFETIME  /  Replay Scope to compare instances", lifetime, -366, 45);
            AddText(content.transform, "Lifetime explanation",
                "Replay Scope: STOPPED 1.2s; scope 0 keeps age, scope 1 resets", -452, 15, false, 24);
            _lifetimeStatusText = AddText(content.transform, "Lifetime live status",
                EffectsDemoLifetime.Summary, -480, 18, false, 64);
            _lifetimeStatusText.color = new Color(0.9f, 0.96f, 1f);
            AddRow(content.transform, "RAINBOW  /  vertex color animation", rainbow, -578, 65, true);
            AddRow(content.transform, "SERIAL  /  A finishes before B begins", serial, -696, 85);
            AddRow(content.transform, "PARALLEL  /  A and B advance together", parallel, -824, 85);
            AddRow(content.transform, "AUDIT  /  property types and marker events in trace", audit, -952, 135, true);
            AddRow(content.transform, "PREPROCESS  /  variables -> markup -> wave", preprocessing,
                -1150, 85, withPreprocessing: true);
            AddRow(content.transform, "PREPROCESS  /  variables -> markup -> wave", preprocessing,
                -1350, 85, true, withPreprocessing: true);
            AddTracePanel(canvasObject.transform);
            UpdateTrace();
            // Newly created RectTransforms receive their final widths on the next canvas pass.
            yield return null;
            UpdateTrace();
        }

        private void AddTracePanel(Transform parent)
        {
            var panel = new GameObject("Execution Trace Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.65f, 0.05f);
            panelRect.anchorMax = new Vector2(0.97f, 0.84f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.22f);

            AddText(panel.transform, "Trace heading", "EXECUTION TRACE  /  time, instance and segment", -12,
                16, false, 56);

            var scrollObject = new GameObject("Trace Scroll View", typeof(RectTransform),
                typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            scrollObject.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRect.anchorMax = new Vector2(0.98f, 0.89f);
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = Vector2.zero;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 100);

            _traceText = AddText(content.transform, "Execution trace", string.Empty, -6, 14, false, 88);
            _traceText.color = new Color(0.85f, 0.9f, 1f);

            _traceScroll = scrollObject.GetComponent<ScrollRect>();
            _traceScroll.viewport = viewportRect;
            _traceScroll.content = contentRect;
            _traceScroll.horizontal = false;
            _traceScroll.vertical = true;
            _traceScroll.movementType = ScrollRect.MovementType.Clamped;
            _traceScroll.scrollSensitivity = 30;
        }

        private void AddRow(Transform parent, string label, string markup, float y, float height,
            bool usePipelineUGUI = false, bool withPreprocessing = false)
        {
            var backend = usePipelineUGUI ? "TextPipelineUGUI" : "TextPipeline";
            AddText(parent, label + " label", backend + "  /  " + label, y, 18, false, 30);
            if (withPreprocessing)
                _preprocessingInputs.Add(AddText(parent, backend + " preprocessing input", "Input: " + markup,
                    y - 34, 16, false, 40));
            var text = AddText(parent, backend + " / " + label + " example", markup,
                y - (withPreprocessing ? 78 : 34), 30, true, height,
                usePipelineUGUI, withPreprocessing ? playerName ?? string.Empty : null);
            _pipelines.Add(usePipelineUGUI
                ? (ITextPipeline)text
                : text.GetComponent<PipelineComponent>());
        }

        private static TMP_Text AddText(Transform parent, string name, string value, float y,
            float fontSize, bool addPipeline, float height, bool usePipelineUGUI = false,
            string sourcePlayerName = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 1f);
            rect.anchorMax = new Vector2(0.92f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(0, height);
            // Sources must exist before either component's Awake caches the source list.
            if (sourcePlayerName != null)
            {
                // Attach in reverse order to demonstrate that Source Sequence controls execution.
                go.AddComponent<DemoMarkupSource>();
                go.AddComponent<DemoVariablesSource>().playerName = sourcePlayerName;
            }
            var text = (TMP_Text)go.AddComponent(usePipelineUGUI
                ? typeof(TextPipelineUGUI)
                : typeof(TextMeshProUGUI));
            if (text is TextPipelineUGUI ugui)
                ugui.processTextOnEnable = false;
            text.fontSize = fontSize;
            text.color = addPipeline ? new Color(0.95f, 0.96f, 1f) : new Color(0.57f, 0.75f, 0.95f);
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            // Assign through TMP_Text while inactive: UGUI processes the pending text on activation.
            text.text = value;

            if (addPipeline && !usePipelineUGUI)
            {
                var pipeline = go.AddComponent<PipelineComponent>();
                pipeline.processTextOnEnable = false;
                pipeline.textMesh = text;
                go.SetActive(true);
                pipeline.SetOriginalTextSimply(value);
            }
            else
            {
                go.SetActive(true);
            }

            return text;
        }

        private static void AddButton(Transform parent, string caption, float x, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(caption + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(x, -22);
            rect.sizeDelta = new Vector2(145, 42);
            go.GetComponent<Image>().color = new Color(0.16f, 0.27f, 0.43f);
            go.GetComponent<Button>().onClick.AddListener(action);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<TextMeshProUGUI>();
            text.text = caption;
            text.fontSize = 19;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        private void Replay()
        {
            CancelScopeReplay();
            EffectsDemoTrace.Clear();
            EffectsDemoLifetime.Clear();
            foreach (var input in _preprocessingInputs)
                input.text = "Input: " + preprocessing;
            var authoredTexts = new[]
                { typer, wave, shake, lifetime, rainbow, serial, parallel, audit, preprocessing, preprocessing };
            for (var i = 0; i < _pipelines.Count; i++)
            {
                var pipeline = _pipelines[i];
                var variables = pipeline.textMesh.GetComponent<DemoVariablesSource>();
                if (variables != null)
                    variables.playerName = playerName;
                if (pipeline is TextPipelineUGUI)
                    pipeline.textMesh.text = authoredTexts[i];
                else
                    pipeline.SetOriginalTextSimply(authoredTexts[i]);
            }
        }

        private void ReplayScope()
        {
            if (_pipelines.Count <= LifetimePipelineIndex || _scopeReplayCoroutine != null) return;
            _scopeReplayCoroutine = StartCoroutine(ReplayScopeSequence());
        }

        private IEnumerator ReplayScopeSequence()
        {
            EffectsDemoTrace.Clear();
            var pipeline = _pipelines[LifetimePipelineIndex];
            ((Behaviour)pipeline).enabled = false;
            EffectsDemoLifetime.StopAll();
            EffectsDemoTrace.Record("Replay Scope: both runs stopped");
            yield return new WaitForSecondsRealtime(1.2f);
            EffectsDemoTrace.Record("Replay Scope: rebuilding");
            ((Behaviour)pipeline).enabled = true;
            pipeline.SetOriginalTextSimply(lifetime);
            _scopeReplayCoroutine = null;
        }

        private void CancelScopeReplay()
        {
            if (_scopeReplayCoroutine == null) return;
            StopCoroutine(_scopeReplayCoroutine);
            _scopeReplayCoroutine = null;
            if (_pipelines.Count > LifetimePipelineIndex)
                ((Behaviour)_pipelines[LifetimePipelineIndex]).enabled = true;
        }

        private void Refresh()
        {
            CancelScopeReplay();
            EffectsDemoTrace.Clear();
            EffectsDemoLifetime.Clear();
            foreach (var pipeline in _pipelines)
                pipeline.Refresh();
        }

        private void UpdateTrace()
        {
            if (_traceText == null) return;

            var trace = EffectsDemoTrace.Text;
            _traceText.text = string.IsNullOrEmpty(trace) ? "Waiting for pipeline events..." : trace;
            if (_traceScroll == null) return;

            var textRect = _traceText.rectTransform;
            var viewportHeight = _traceScroll.viewport.rect.height;
            if (textRect.rect.width < 1f || viewportHeight < 1f) return;

            var preferredHeight = _traceText.GetPreferredValues(
                _traceText.text, textRect.rect.width, 0f).y;
            var contentHeight = Mathf.Max(viewportHeight, preferredHeight + 12f);
            _traceScroll.content.sizeDelta = new Vector2(0f, contentHeight);
            textRect.sizeDelta = new Vector2(0f, contentHeight - 12f);
            _traceScroll.StopMovement();
            _traceScroll.content.anchoredPosition =
                new Vector2(0f, Mathf.Max(0f, contentHeight - viewportHeight));
        }
    }
}
