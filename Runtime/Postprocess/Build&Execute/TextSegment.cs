using System;
using System.Collections;
using System.Collections.Generic;

namespace TextPipeline.Postprocess
{
    public sealed class TextSegment
    {
        private readonly KeyValuePair<string, object>[] _arguments;
        private readonly string _method;
        private readonly Dictionary<string, object> _properties;
        private readonly TextInfoRange _range;

        public TextSegment(Dictionary<string, object> properties, TextInfoRange range)
        {
            _properties = new Dictionary<string, object>(properties);
            _range = range;
        }

        public TextSegment(Dictionary<string, object> properties, string method,
            KeyValuePair<string, object>[] arguments)
        {
            _properties = new Dictionary<string, object>(properties);
            _method = method;
            _arguments = arguments;
        }

        // DoEffect 由 PostProcess 内部调用
        public IEnumerator DoEffect(ITextSinkCoroutine sink, Func<TextInfoRange, IEnumerator> process, CoroutineType coroutineType = CoroutineType.Serial)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            // Segment behavior is independent of how PostProcess schedules it.
            // Returning the scoped enumerator directly also lets callers that
            // manually drive this IEnumerator advance the actual segment body.
            return new ScopedSegmentEnumerator(
                sink,
                _properties,
                ExecuteCoroutineBody(sink, process));
        }

        private IEnumerator ExecuteCoroutineBody(ITextSinkCoroutine sink, Func<TextInfoRange, IEnumerator> process)
        {
            if (_range.CharacterCount != 0)
                yield return process(_range);

            if (_method != null && MarkupInject.MethodInjectWithInvoke(sink, _method, _arguments) is IEnumerator nested)
                yield return nested;
        }

        public void DoEffect(ITextSinkBase sink, Action<TextInfoRange> process)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            var snapshot = MarkupInject.GetSinkSnapshot(sink);

            try
            {
                MarkupInject.InjectBySnapShot(sink, _properties);

                if (_range.CharacterCount != 0)
                {
                    process(_range);
                }
                else if (_method != null)
                {
                    MarkupInject.MethodInjectWithInvoke(
                        sink, _method, _arguments);
                }
            }
            finally
            {
                MarkupInject.InjectBySnapShot(sink, snapshot);
            }
        }

        public enum CoroutineType
        {
            Serial,
            Parallel
        }

        /// <summary>
        /// 一个 DoEffect 调用拥有一份独立的属性状态。
        /// 每次 MoveNext 只在执行该段代码期间把状态放到共享 sink 上。
        /// </summary>
        private sealed class ScopedSegmentEnumerator : IEnumerator, IDisposable
        {
            private readonly ITextSinkCoroutine _sink;
            private readonly Stack<IEnumerator> _stack = new();
            private bool _completed;
            private object _current;

            private Dictionary<string, object> _segmentProperties;

            public ScopedSegmentEnumerator(
                ITextSinkCoroutine sink,
                Dictionary<string, object> properties,
                IEnumerator body)
            {
                _sink = sink;
                _segmentProperties = new Dictionary<string, object>(properties);
                _stack.Push(body);
            }

            public void Dispose()
            {
                if (_completed)
                    return;

                var previous = MarkupInject.GetSinkSnapshot(_sink);
                try
                {
                    MarkupInject.InjectBySnapShot(_sink, _segmentProperties);

                    _completed = true;
                    _current = null;
                    DisposeAll();
                }
                finally
                {
                    MarkupInject.InjectBySnapShot(_sink, previous);
                }
            }

            public object Current => _current;

            public bool MoveNext()
            {
                if (_completed)
                    return false;

                var previous = MarkupInject.GetSinkSnapshot(_sink);

                try
                {
                    MarkupInject.InjectBySnapShot(_sink, _segmentProperties);

                    // 自己推进嵌套 IEnumerator，确保 process(_range)
                    // 和方法标记返回的迭代器也处于同一个属性作用域。
                    while (_stack.Count > 0)
                    {
                        var iterator = _stack.Peek();

                        if (!iterator.MoveNext())
                        {
                            DisposeTop();
                            continue;
                        }

                        var yielded = iterator.Current;

                        if (yielded is IEnumerator nested)
                        {
                            _stack.Push(nested);
                            continue;
                        }

                        _current = yielded;
                        return true;
                    }

                    _completed = true;
                    _current = null;
                    return false;
                }
                catch (Exception executionError)
                {
                    _completed = true;
                    _current = null;

                    try
                    {
                        DisposeAll();
                    }
                    catch (Exception cleanupError)
                    {
                        throw new AggregateException(executionError, cleanupError);
                    }

                    throw;
                }
                finally
                {
                    try
                    {
                        // 回调若修改了注入属性，修改只归此 Segment 所有，
                        // 下次推进该 Segment 时继续使用。
                        _segmentProperties = MarkupInject.GetSinkSnapshot(_sink);
                    }
                    finally
                    {
                        MarkupInject.InjectBySnapShot(_sink, previous);
                    }
                }
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            private void DisposeTop()
            {
                var iterator = _stack.Pop();

                if (iterator is IDisposable disposable)
                    disposable.Dispose();
            }

            private void DisposeAll()
            {
                List<Exception> errors = null;

                while (_stack.Count > 0)
                {
                    try
                    {
                        DisposeTop();
                    }
                    catch (Exception error)
                    {
                        errors ??= new List<Exception>();
                        errors.Add(error);
                    }
                }

                if (errors != null)
                    throw new AggregateException(errors);
            }
        }
    }
}
