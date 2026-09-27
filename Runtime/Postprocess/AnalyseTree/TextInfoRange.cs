using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace TextPipeline.Postprocess
{
    public readonly struct TextInfoRange : IEnumerable<TMP_CharacterInfo>
    {
        private TMP_TextInfo TextInfo { get; }
        private int StartIndex { get; }
        private int EndIndex { get; }
        public int CharacterCount => EndIndex - StartIndex;

        public TextInfoRange(TMP_TextInfo textInfo, int startIndex, int endIndex)
        {
            TextInfo = textInfo ?? throw new ArgumentNullException(nameof(textInfo));
            if (startIndex < 0 || endIndex < startIndex || endIndex > textInfo.characterCount)
                throw new ArgumentOutOfRangeException(nameof(endIndex));

            StartIndex = startIndex;
            EndIndex = endIndex;
        }

        public ref TMP_CharacterInfo CharacterAt(int localIndex)
        {
            if ((uint)localIndex >= (uint)CharacterCount)
                throw new ArgumentOutOfRangeException(nameof(localIndex));

            return ref TextInfo.characterInfo[StartIndex + localIndex];
        }

        /// <summary>
        /// 返回一个无分配的枚举器。普通 <c>foreach</c> 读取字符副本；使用
        /// <c>foreach (ref var character in range)</c> 可直接修改原始 characterInfo 数组。
        /// </summary>
        public Enumerator GetEnumerator() => new(TextInfo, StartIndex, EndIndex);

        IEnumerator<TMP_CharacterInfo> IEnumerable<TMP_CharacterInfo>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<TMP_CharacterInfo>
        {
            private readonly TMP_TextInfo _textInfo;
            private readonly int _endIndex;
            private int _currentIndex;

            internal Enumerator(TMP_TextInfo textInfo, int startIndex, int endIndex)
            {
                _textInfo = textInfo;
                _endIndex = endIndex;
                _currentIndex = startIndex - 1;
            }

            public ref TMP_CharacterInfo Current => ref _textInfo.characterInfo[_currentIndex];

            TMP_CharacterInfo IEnumerator<TMP_CharacterInfo>.Current => Current;

            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_currentIndex < _endIndex;

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
            }
        }
    }
}