using System;
using System.Collections;
using TextPipeline.Postprocess;
using TMPro;

namespace TextPipeline
{
    /// <summary>Source/Sink 使用的管线宿主；可由挂载组件或 TMP 派生组件提供。</summary>
    public interface ITextPipeline
    {
        TMP_Text textMesh { get; }
        string OriginalText { get; }
        string SetOriginalText(string text, bool withPreProcessing = true, bool withPostProcessing = true);
        void SetOriginalTextSimply(string text);
        void Refresh();
        void StartSinkCoroutine(Func<TextSegment[], IEnumerator> postProcess, TextSegment[] steps);
    }
}
