using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Superpower;
using Superpower.Model;
using Superpower.Parsers;
using TMPro;

namespace TextPipeline.Postprocess
{
    /// <summary>
    /// Parses the TextPipeline markup language into the node tree used by post-processing.
    /// The lexical productions for tag candidates are expressed with Superpower; the document
    /// driver owns the HTML-style stack recovery specified by <c>Error Handling.md</c>.
    /// </summary>
    public static class TextInfoMarkupParser
    {
        private static readonly TextParser<Unit> WhiteSpace = Character.WhiteSpace.IgnoreMany();

        private static readonly TextParser<string> Name =
            from first in Character.Lower
            from rest in Character.Lower.Or(Character.Digit).Or(Character.In('_', '-')).Many()
            select first + new string(rest.ToArray());

        private static readonly TextParser<string> Digits = Character.Digit.AtLeastOnce()
            .Select(characters => new string(characters.ToArray()));

        private static readonly TextParser<string> Number =
            from sign in Character.EqualTo('-').OptionalOrDefault('\0')
            from integerPart in Digits
            from fractionalPart in Character.EqualTo('.').IgnoreThen(Digits).OptionalOrDefault(string.Empty)
            select sign == '\0'
                ? integerPart + (fractionalPart.Length == 0 ? string.Empty : "." + fractionalPart)
                : "-" + integerPart + (fractionalPart.Length == 0 ? string.Empty : "." + fractionalPart);

        private static readonly TextParser<char> EscapedStringCharacter = Character.EqualTo('\\')
            .IgnoreThen(Character.In('"', '\\'));

        private static readonly TextParser<char> StringCharacter = EscapedStringCharacter.Try()
            .Or(Character.ExceptIn('"', '\\'));

        private static readonly TextParser<ParsedValue> StringValue =
            from openingQuote in Character.EqualTo('"')
            from characters in StringCharacter.Many()
            from closingQuote in Character.EqualTo('"')
            select new ParsedValue(ValueKind.String, new string(characters.ToArray()));

        private static readonly TextParser<ParsedValue> Value = StringValue
            .Or(Number.Select(value => new ParsedValue(ValueKind.Number, value)))
            .Or(Span.EqualTo("true").Value(new ParsedValue(ValueKind.Boolean, "true")))
            .Or(Span.EqualTo("false").Value(new ParsedValue(ValueKind.Boolean, "false")));

        private static readonly TextParser<string> NameToken = Token(Name);
        private static readonly TextParser<ParsedValue> ValueToken = Token(Value);
        private static readonly TextParser<string> IntegerToken = Token(Digits);
        private static readonly TextParser<char> Colon = Token(Character.EqualTo(':'));
        private static readonly TextParser<char> Comma = Token(Character.EqualTo(','));
        private static readonly TextParser<char> EqualsSign = Token(Character.EqualTo('='));
        private static readonly TextParser<char> Pipe = Token(Character.EqualTo('|'));
        private static readonly TextParser<char> LeftParenthesis = Token(Character.EqualTo('('));
        private static readonly TextParser<char> RightParenthesis = Token(Character.EqualTo(')'));
        private static readonly TextParser<char> RightAngle = Character.EqualTo('>');

        private static readonly TextParser<NamedValue> Property =
            from name in NameToken
            from equalsSign in EqualsSign
            from value in ValueToken
            select new NamedValue(name, value);

        private static readonly TextParser<NamedValue[]> PropertyList = Colon.IgnoreThen(
            Property.AtLeastOnceDelimitedBy(Comma).Select(values => values.ToArray()));

        private static readonly TextParser<NamedValue> NamedParameter =
            from name in NameToken
            from colon in Colon
            from value in ValueToken
            select new NamedValue(name, value);

        private static readonly TextParser<NamedValue[]> NamedParameterList = NamedParameter
            .AtLeastOnceDelimitedBy(Comma)
            .Select(values => values.ToArray());

        private static readonly TextParser<ParsedParameters> Parameters =
            from openingParenthesis in LeftParenthesis
            from parameters in NamedParameterList.Try()
                .Select(values => ParsedParameters.Named(values))
                .Or(ValueToken.Select(value => ParsedParameters.Single(value)))
            from closingParenthesis in RightParenthesis
            select parameters;

        private static readonly TextParser<string> ScopePart =
            from pipe in Pipe
            from scope in Token(Span.EqualTo("scope"))
            from equalsSign in EqualsSign
            from value in IntegerToken
            select value;

        private static readonly TextParser<ParsedBlockStart> BlockStart =
            from openingAngle in Character.EqualTo('<')
            from markup in NameToken
            from properties in PropertyList.OptionalOrDefault(Array.Empty<NamedValue>())
            from scope in ScopePart.OptionalOrDefault(null)
            from closingAngle in RightAngle
            select new ParsedBlockStart(markup, properties, scope);

        private static readonly TextParser<ParsedSingleMarker> SingleMarker =
            from openingAngle in Character.EqualTo('<')
            from markup in NameToken
            from colon in Colon
            from method in NameToken
            from parameters in Parameters.OptionalOrDefault(ParsedParameters.None)
            from scope in ScopePart.OptionalOrDefault(null)
            from closingAngle in RightAngle
            select new ParsedSingleMarker(markup, method, parameters, scope);

        private static readonly TextParser<string> BlockEnd =
            from openingAngle in Character.EqualTo('<')
            from slash in Character.EqualTo('/')
            from markup in NameToken
            from closingAngle in RightAngle
            select markup;

        /// <summary>
        /// Superpower entry point for string input. It always completes the document by applying
        /// the language's tag-level recovery rules; diagnostics are exposed from the returned root.
        /// </summary>
        public static readonly TextParser<RootNode> Parser = ParseDocument;

        /// <summary>
        /// Parses authoring text. This overload creates a character snapshot for callers that do
        /// not already have TMP text information (for example, EditMode tests and import tools).
        /// </summary>
        public static RootNode Parse(string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Parser.Parse(source);
        }

        /// <summary>
        /// Parses a TextMeshPro character snapshot. Successful markup is removed from the formal
        /// text represented by ContentNode.Content, so node indices use that compacted coordinate
        /// system rather than the input snapshot's coordinates.
        /// </summary>
        public static RootNode Parse(TMP_TextInfo textInfo)
        {
            if (textInfo == null) throw new ArgumentNullException(nameof(textInfo));
            if (textInfo.characterCount < 0 || textInfo.characterInfo == null ||
                textInfo.characterCount > textInfo.characterInfo.Length)
                throw new ArgumentException("TMP character information is incomplete.", nameof(textInfo));

            var snapshot = new TMP_CharacterInfo[textInfo.characterCount];
            Array.Copy(textInfo.characterInfo, snapshot, snapshot.Length);
            return ParseSnapshot(snapshot, textInfo.textComponent);
        }

        /// <summary>
        /// Builds a markup plan from TMP's authoring-text output without binding any content range
        /// to that transient output. The plan removes TextPipeline marker spans from the original
        /// source while preserving TMP's own rich-text markup, then can bind its ranges to the
        /// TextInfo produced from that formalised source.
        /// </summary>
        public static MarkupParsePlan Analyse(string source, TMP_TextInfo authoringTextInfo)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (authoringTextInfo == null) throw new ArgumentNullException(nameof(authoringTextInfo));
            if (authoringTextInfo.characterCount < 0 || authoringTextInfo.characterInfo == null ||
                authoringTextInfo.characterCount > authoringTextInfo.characterInfo.Length)
                throw new ArgumentException("TMP character information is incomplete.", nameof(authoringTextInfo));

            var snapshot = new TMP_CharacterInfo[authoringTextInfo.characterCount];
            Array.Copy(authoringTextInfo.characterInfo, snapshot, snapshot.Length);

            var characters = new char[snapshot.Length];
            for (int index = 0; index < snapshot.Length; index++)
                characters[index] = snapshot[index].character;

            var removedCandidateSpans = new List<SourceSpan>();
            var expectedFormalCharacters = new List<TMP_CharacterInfo>();
            RootNode root = ParseSource(new string(characters), snapshot, authoringTextInfo.textComponent, null,
                removedCandidateSpans, expectedFormalCharacters);
            string formalisedText = RemoveSourceSpans(source, snapshot, removedCandidateSpans);
            return new MarkupParsePlan(root, formalisedText, expectedFormalCharacters.ToArray());
        }

        /// <summary>
        /// Parses a TMP character snapshot. The array is copied so later mesh updates cannot make
        /// the tree refer to a different character sequence.
        /// </summary>
        public static RootNode Parse(TMP_CharacterInfo[] characterInfo)
        {
            if (characterInfo == null) throw new ArgumentNullException(nameof(characterInfo));

            var snapshot = new TMP_CharacterInfo[characterInfo.Length];
            Array.Copy(characterInfo, snapshot, snapshot.Length);
            return ParseSnapshot(snapshot, null);
        }

        /// <summary>
        /// Parses authoring text and injects the TextMeshPro snapshot for the formal
        /// (marker-stripped) text. Every ContentNode stores a TextInfoRange over the same
        /// reference, limited by its own <c>[StartIndex, EndIndex)</c>.
        /// </summary>
        public static RootNode Parse(string source, TMP_TextInfo markerStrippedTextInfo)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (markerStrippedTextInfo == null) throw new ArgumentNullException(nameof(markerStrippedTextInfo));

            return ParseSource(source, CreateSnapshot(source), null, markerStrippedTextInfo);
        }

        private static TextParser<T> Token<T>(TextParser<T> parser)
        {
            return parser.Then(value => WhiteSpace.Value(value));
        }

        private static Result<RootNode> ParseDocument(TextSpan input)
        {
            string source = input.ToStringValue();
            RootNode rootNode = ParseSource(source, CreateSnapshot(source), null, null);
            return Result.Value(rootNode, input.Skip(source.Length), input);
        }

        private static RootNode ParseSnapshot(TMP_CharacterInfo[] snapshot, TMP_Text textComponent)
        {
            var characters = new char[snapshot.Length];
            for (int index = 0; index < snapshot.Length; index++)
                characters[index] = snapshot[index].character;

            return ParseSource(new string(characters), snapshot, textComponent, null);
        }

        private static RootNode ParseSource(string source, TMP_CharacterInfo[] sourceSnapshot, TMP_Text textComponent,
            TMP_TextInfo injectedTextInfo, List<SourceSpan> removedCandidateSpans = null,
            List<TMP_CharacterInfo> expectedFormalCharacters = null)
        {
            var root = new RootNode { StartIndex = 0 };
            var openBlocks = new List<BlockFrame>();
            var retainedCharacters = new List<TMP_CharacterInfo>();
            var contentNodes = new List<ContentNode>();
            int contentStart = 0;
            int formalTextIndex = 0;
            int index = 0;

            while (index < source.Length)
            {
                char character = source[index];
                if (character == '<' && LooksLikeTagCandidate(source, index))
                {
                    AddContent(root, openBlocks, sourceSnapshot, contentStart, index, ref formalTextIndex,
                        retainedCharacters, contentNodes);
                    int candidateStartIndex = index;
                    ParseTagCandidate(source, root, openBlocks, formalTextIndex, ref index);
                    if (removedCandidateSpans != null && index > candidateStartIndex)
                        removedCandidateSpans.Add(new SourceSpan(candidateStartIndex, index));
                    contentStart = index;
                    continue;
                }

                if (character == '&')
                {
                    if (index + 1 < source.Length &&
                        (source[index + 1] == '<' || source[index + 1] == '>' || source[index + 1] == '&'))
                    {
                        index += 2;
                        continue;
                    }

                    root.AddDiagnostic(MarkupParseErrorKind.Lexical, index, index + 1,
                        "正文中的 & 必须使用 &&、&< 或 &> 转义。");
                }
                else if (character == '>')
                {
                    root.AddDiagnostic(MarkupParseErrorKind.Lexical, index, index + 1,
                        "正文中的 > 必须使用 &> 转义。");
                }

                index++;
            }

            AddContent(root, openBlocks, sourceSnapshot, contentStart, source.Length, ref formalTextIndex,
                retainedCharacters, contentNodes);
            CloseOpenBlocksAtEnd(openBlocks, formalTextIndex);
            root.EndIndex = formalTextIndex;

            expectedFormalCharacters?.AddRange(retainedCharacters);

            TMP_TextInfo formalTextInfo = injectedTextInfo == null
                ? CreateFormalTextInfo(retainedCharacters, textComponent)
                : ValidateInjectedTextInfo(injectedTextInfo, retainedCharacters);
            foreach (ContentNode contentNode in contentNodes)
                contentNode.Content = new TextInfoRange(
                    formalTextInfo, contentNode.StartIndex, contentNode.EndIndex);

            return root;
        }

        private static string RemoveSourceSpans(string source, TMP_CharacterInfo[] sourceSnapshot,
            IReadOnlyList<SourceSpan> sourceSpans)
        {
            if (sourceSpans.Count == 0) return source;

            var rawSpans = new List<SourceSpan>(sourceSpans.Count);
            foreach (SourceSpan span in sourceSpans)
            {
                if (span.StartIndex < 0 || span.EndIndex <= span.StartIndex ||
                    span.EndIndex > sourceSnapshot.Length)
                    throw new ArgumentException("Markup candidate span is outside the TMP character snapshot.");

                TMP_CharacterInfo first = sourceSnapshot[span.StartIndex];
                TMP_CharacterInfo last = sourceSnapshot[span.EndIndex - 1];
                int rawStartIndex = first.index;
                int rawEndIndex = last.index + Math.Max(last.stringLength, 1);
                if (rawStartIndex < 0 || rawEndIndex < rawStartIndex || rawEndIndex > source.Length)
                    throw new ArgumentException(
                        "TMP character information cannot be mapped back to the authoring source.",
                        nameof(source));

                rawSpans.Add(new SourceSpan(rawStartIndex, rawEndIndex));
            }

            rawSpans.Sort((left, right) => left.StartIndex.CompareTo(right.StartIndex));
            var builder = new StringBuilder(source.Length);
            int copiedThrough = 0;
            foreach (SourceSpan span in rawSpans)
            {
                if (span.StartIndex < copiedThrough)
                    throw new ArgumentException("Markup candidate spans overlap in the authoring source.", nameof(source));

                builder.Append(source, copiedThrough, span.StartIndex - copiedThrough);
                copiedThrough = span.EndIndex;
            }

            builder.Append(source, copiedThrough, source.Length - copiedThrough);
            return builder.ToString();
        }

        private static TMP_CharacterInfo[] CreateSnapshot(string source)
        {
            var snapshot = new TMP_CharacterInfo[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                snapshot[index].character = source[index];
                snapshot[index].index = index;
                snapshot[index].stringLength = 1;
            }
            return snapshot;
        }

        private static TMP_TextInfo CreateFormalTextInfo(IReadOnlyList<TMP_CharacterInfo> retainedCharacters,
            TMP_Text textComponent)
        {
            var textInfo = new TMP_TextInfo
            {
                textComponent = textComponent,
                characterCount = retainedCharacters.Count,
                characterInfo = new TMP_CharacterInfo[retainedCharacters.Count]
            };

            for (int index = 0; index < retainedCharacters.Count; index++)
                textInfo.characterInfo[index] = retainedCharacters[index];

            return textInfo;
        }

        private static TMP_TextInfo ValidateInjectedTextInfo(TMP_TextInfo injectedTextInfo,
            IReadOnlyList<TMP_CharacterInfo> retainedCharacters)
        {
            if (injectedTextInfo.characterInfo == null || injectedTextInfo.characterCount < 0 ||
                injectedTextInfo.characterCount > injectedTextInfo.characterInfo.Length)
                throw new ArgumentException("Injected TMP text information is incomplete.", nameof(injectedTextInfo));

            if (injectedTextInfo.characterCount != retainedCharacters.Count)
                throw new ArgumentException(
                    "Injected TMP text information must have the same character count as the marker-stripped text.",
                    nameof(injectedTextInfo));

            for (int characterIndex = 0; characterIndex < retainedCharacters.Count; characterIndex++)
            {
                if (injectedTextInfo.characterInfo[characterIndex].character != retainedCharacters[characterIndex].character)
                    throw new ArgumentException(
                        "Injected TMP text information must contain the marker-stripped character sequence.",
                        nameof(injectedTextInfo));
            }

            return injectedTextInfo;
        }

        private static bool LooksLikeTagCandidate(string source, int index)
        {
            if (index + 1 >= source.Length) return false;

            char next = source[index + 1];
            return next == '/' || IsAsciiLetter(next);
        }

        private static bool IsAsciiLetter(char character)
        {
            return character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z';
        }

        private static void ParseTagCandidate(string source, RootNode rootNode, List<BlockFrame> openBlocks,
            int formalTextIndex, ref int index)
        {
            int startIndex = index;
            var input = new TextSpan(source.Substring(index));

            if (source[index + 1] == '/')
            {
                Result<string> endResult = BlockEnd(input);
                if (!endResult.HasValue)
                {
                    RecoverInvalidTag(source, rootNode, ref index, startIndex, "结束标签不符合语法。");
                    return;
                }

                int endIndex = GetEndIndex(source, startIndex, endResult.Remainder);
                CloseBlock(rootNode, openBlocks, endResult.Value, startIndex, endIndex, formalTextIndex);
                index = endIndex;
                return;
            }

            Result<ParsedSingleMarker> singleResult = SingleMarker(input);
            if (singleResult.HasValue)
            {
                int endIndex = GetEndIndex(source, startIndex, singleResult.Remainder);
                if (!TryCreateSingleMarker(singleResult.Value, formalTextIndex, out SingleMarkerNode marker,
                        out MarkupParseErrorKind errorKind, out string errorMessage))
                {
                    rootNode.AddDiagnostic(errorKind, startIndex, endIndex, errorMessage);
                }
                else
                {
                    AddNode(rootNode, openBlocks, marker);
                }

                index = endIndex;
                return;
            }

            Result<ParsedBlockStart> blockResult = BlockStart(input);
            if (blockResult.HasValue)
            {
                int endIndex = GetEndIndex(source, startIndex, blockResult.Remainder);
                if (!TryCreateBlock(blockResult.Value, formalTextIndex, out BlockNode block,
                        out MarkupParseErrorKind errorKind, out string errorMessage))
                {
                    rootNode.AddDiagnostic(errorKind, startIndex, endIndex, errorMessage);
                }
                else
                {
                    AddNode(rootNode, openBlocks, block);
                    openBlocks.Add(new BlockFrame(block));
                }

                index = endIndex;
                return;
            }

            RecoverInvalidTag(source, rootNode, ref index, startIndex, "标签不符合语法。");
        }

        private static int GetEndIndex(string source, int startIndex, TextSpan remainder)
        {
            return source.Length - remainder.Length;
        }

        private static void RecoverInvalidTag(string source, RootNode rootNode, ref int index, int startIndex, string message)
        {
            int endIndex = FindTagRecoveryBoundary(source, startIndex);
            rootNode.AddDiagnostic(GuessCandidateErrorKind(source, startIndex), startIndex, endIndex, message);
            index = endIndex;
        }

        private static MarkupParseErrorKind GuessCandidateErrorKind(string source, int startIndex)
        {
            int cursor = startIndex + 1;
            if (cursor < source.Length && source[cursor] == '/') cursor++;
            return cursor < source.Length && source[cursor] >= 'A' && source[cursor] <= 'Z'
                ? MarkupParseErrorKind.Lexical
                : MarkupParseErrorKind.Syntax;
        }

        private static int FindTagRecoveryBoundary(string source, int startIndex)
        {
            bool quoted = false;
            for (int index = startIndex + 1; index < source.Length; index++)
            {
                char character = source[index];
                if (quoted && character == '\\' && index + 1 < source.Length)
                {
                    index++;
                    continue;
                }

                if (character == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && character == '>') return index + 1;
            }

            return source.Length;
        }

        private static bool TryCreateBlock(ParsedBlockStart parsed, int formalTextIndex, out BlockNode block,
            out MarkupParseErrorKind errorKind, out string errorMessage)
        {
            block = null;
            if (!TryValidateMarkup(parsed.Markup, parsed.ScopeText, out int scope,
                    out errorKind, out errorMessage))
                return false;
            if (!TryValidatePairs(parsed.Markup, null, parsed.Properties, out errorKind, out errorMessage))
                return false;

            block = new BlockNode
            {
                Markup = parsed.Markup,
                PropertyPairs = ToPairs(parsed.Properties),
                ScopeNum = scope,
                StartIndex = formalTextIndex
            };
            return true;
        }

        private static bool TryCreateSingleMarker(ParsedSingleMarker parsed, int formalTextIndex,
            out SingleMarkerNode marker, out MarkupParseErrorKind errorKind, out string errorMessage)
        {
            marker = null;
            if (!TryValidateMarkup(parsed.Markup, parsed.ScopeText, out int scope,
                    out errorKind, out errorMessage))
                return false;

            if (MarkupRegistry.LookupForMethod(parsed.Markup, parsed.Method) == null)
            {
                errorKind = MarkupParseErrorKind.Contextual;
                errorMessage = $"标签 <{parsed.Markup}> 未声明函数 \"{parsed.Method}\"。";
                return false;
            }

            NamedValue[] parameterPairs = parsed.Parameters.IsSingleValue
                ? BindSingleValue(parsed.Markup, parsed.Method, parsed.Parameters.SingleValue, out errorKind,
                    out errorMessage)
                : parsed.Parameters.NamedValues;
            if (parameterPairs == null) return false;

            if (!TryValidatePairs(parsed.Markup, parsed.Method, parameterPairs, out errorKind, out errorMessage))
                return false;

            marker = new SingleMarkerNode
            {
                Markup = parsed.Markup,
                MethodName = parsed.Method,
                ParamPairs = ToPairs(parameterPairs),
                ScopeNum = scope,
                StartIndex = formalTextIndex,
                EndIndex = formalTextIndex
            };
            return true;
        }

        private static NamedValue[] BindSingleValue(string markup, string method, ParsedValue value,
            out MarkupParseErrorKind errorKind, out string errorMessage)
        {
            var parameter = MarkupRegistry.LookupForParam(markup, method);
            if (parameter.param == null)
            {
                errorKind = MarkupParseErrorKind.Contextual;
                errorMessage = $"函数 {markup}:{method} 不能使用无参数名的单值形式。";
                return null;
            }

            errorKind = default;
            errorMessage = null;
            return new[] { new NamedValue(parameter.paramName, value) };
        }

        private static bool TryValidateMarkup(string markup, string scopeText, out int scope,
            out MarkupParseErrorKind errorKind, out string errorMessage)
        {
            scope = 0;
            if (MarkupRegistry.LookupForType(markup) == null)
            {
                errorKind = MarkupParseErrorKind.Contextual;
                errorMessage = $"未注册的标签 <{markup}>。";
                return false;
            }

            if (scopeText != null &&
                !int.TryParse(scopeText, NumberStyles.None, CultureInfo.InvariantCulture, out scope))
            {
                errorKind = MarkupParseErrorKind.Contract;
                errorMessage = "scope 必须是 Int32 范围内的非负整数。";
                return false;
            }

            errorKind = default;
            errorMessage = null;
            return true;
        }

        private static bool TryValidatePairs(string markup, string method, NamedValue[] pairs,
            out MarkupParseErrorKind errorKind, out string errorMessage)
        {
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (NamedValue pair in pairs)
            {
                if (!seenNames.Add(pair.Name))
                {
                    errorKind = MarkupParseErrorKind.Contextual;
                    errorMessage = $"{(method == null ? "属性" : "参数")} \"{pair.Name}\" 重复出现。";
                    return false;
                }

                MarkupDataType dataType;
                Type targetType;
                if (method == null)
                {
                    var property = MarkupRegistry.LookupForProperty(markup, pair.Name);
                    if (property.property == null)
                    {
                        errorKind = MarkupParseErrorKind.Contextual;
                        errorMessage = $"标签 <{markup}> 不接受属性 \"{pair.Name}\"。";
                        return false;
                    }

                    dataType = property.dataType;
                    targetType = property.property.PropertyType;
                }
                else
                {
                    var parameter = MarkupRegistry.LookupForParam(markup, method, pair.Name);
                    if (parameter.param == null)
                    {
                        errorKind = MarkupParseErrorKind.Contextual;
                        errorMessage = $"函数 {markup}:{method} 不接受参数 \"{pair.Name}\"。";
                        return false;
                    }

                    dataType = parameter.dataType;
                    targetType = parameter.param.ParameterType;
                }

                if (!ValueMatches(dataType, targetType, pair.Value))
                {
                    errorKind = MarkupParseErrorKind.Contract;
                    errorMessage = $"{(method == null ? "属性" : "参数")} \"{pair.Name}\" 的值类型不匹配。";
                    return false;
                }
            }

            if (method == null)
            {
                foreach (var property in MarkupRegistry.LookupForDeclaredProperties(markup))
                {
                    if (property.allowDefault || seenNames.Contains(property.name)) continue;
                    errorKind = MarkupParseErrorKind.Contract;
                    errorMessage = $"标签 <{markup}> 的属性 \"{property.name}\" 不允许省略。";
                    return false;
                }
            }
            else
            {
                foreach (var parameter in MarkupRegistry.LookupForDeclaredParams(markup, method))
                {
                    if (parameter.allowDefault || seenNames.Contains(parameter.name)) continue;
                    errorKind = MarkupParseErrorKind.Contract;
                    errorMessage = $"函数 {markup}:{method} 的参数 \"{parameter.name}\" 不允许省略。";
                    return false;
                }
            }

            errorKind = default;
            errorMessage = null;
            return true;
        }

        private static bool ValueMatches(MarkupDataType dataType, Type targetType, ParsedValue value)
        {
            if (dataType == MarkupDataType.String) return value.Kind == ValueKind.String;
            if (dataType == MarkupDataType.Boolean) return value.Kind == ValueKind.Boolean;
            if (dataType != MarkupDataType.Number || value.Kind != ValueKind.Number) return false;
            if (targetType == typeof(int))
                return int.TryParse(value.Text, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out _);

            return float.TryParse(value.Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                       CultureInfo.InvariantCulture, out float number) &&
                   !float.IsNaN(number) && !float.IsInfinity(number);
        }

        private static KeyValuePair<string, string>[] ToPairs(IEnumerable<NamedValue> values)
        {
            return values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value.Text)).ToArray();
        }

        private static void AddContent(RootNode rootNode, List<BlockFrame> openBlocks,
            TMP_CharacterInfo[] sourceSnapshot, int sourceStartIndex, int sourceEndIndex, ref int formalTextIndex,
            List<TMP_CharacterInfo> retainedCharacters, List<ContentNode> contentNodes)
        {
            if (sourceStartIndex >= sourceEndIndex) return;

            int contentStartIndex = formalTextIndex;
            for (int sourceIndex = sourceStartIndex; sourceIndex < sourceEndIndex; sourceIndex++)
            {
                TMP_CharacterInfo characterInfo = sourceSnapshot[sourceIndex];
                characterInfo.index = formalTextIndex;
                retainedCharacters.Add(characterInfo);
                formalTextIndex++;
            }

            var contentNode = new ContentNode
            {
                StartIndex = contentStartIndex,
                EndIndex = formalTextIndex
            };
            AddNode(rootNode, openBlocks, contentNode);
            contentNodes.Add(contentNode);
        }

        private static void AddNode(RootNode rootNode, List<BlockFrame> openBlocks, Node node)
        {
            BlockNode parent = openBlocks.Count == 0 ? rootNode : openBlocks[openBlocks.Count - 1].Node;
            node.Parent = parent;
            parent.Children.Add(node);
        }

        private static void CloseBlock(RootNode rootNode, List<BlockFrame> openBlocks, string markup,
            int closingSourceStartIndex, int closingSourceEndIndex, int formalTextIndex)
        {
            int matchingIndex = -1;
            for (int index = openBlocks.Count - 1; index >= 0; index--)
            {
                if (string.Equals(openBlocks[index].Node.Markup, markup, StringComparison.Ordinal))
                {
                    matchingIndex = index;
                    break;
                }
            }

            if (matchingIndex < 0)
            {
                rootNode.AddDiagnostic(MarkupParseErrorKind.Contextual, closingSourceStartIndex, closingSourceEndIndex,
                    $"孤立的结束标签 </{markup}> 已忽略。");
                return;
            }

            for (int index = openBlocks.Count - 1; index > matchingIndex; index--)
            {
                openBlocks[index].Node.EndIndex = formalTextIndex;
                openBlocks.RemoveAt(index);
            }

            openBlocks[matchingIndex].Node.EndIndex = formalTextIndex;
            openBlocks.RemoveAt(matchingIndex);
        }

        private static void CloseOpenBlocksAtEnd(List<BlockFrame> openBlocks, int endIndex)
        {
            for (int index = openBlocks.Count - 1; index >= 0; index--)
                openBlocks[index].Node.EndIndex = endIndex;
        }

        private enum ValueKind
        {
            String,
            Number,
            Boolean
        }

        private readonly struct ParsedValue
        {
            public readonly ValueKind Kind;
            public readonly string Text;

            public ParsedValue(ValueKind kind, string text)
            {
                Kind = kind;
                Text = text;
            }
        }

        private readonly struct NamedValue
        {
            public readonly string Name;
            public readonly ParsedValue Value;

            public NamedValue(string name, ParsedValue value)
            {
                Name = name;
                Value = value;
            }
        }

        private readonly struct ParsedParameters
        {
            public static readonly ParsedParameters None = new(false, default, Array.Empty<NamedValue>());

            public readonly bool IsSingleValue;
            public readonly ParsedValue SingleValue;
            public readonly NamedValue[] NamedValues;

            private ParsedParameters(bool isSingleValue, ParsedValue singleValue, NamedValue[] namedValues)
            {
                IsSingleValue = isSingleValue;
                SingleValue = singleValue;
                NamedValues = namedValues;
            }

            public static ParsedParameters Single(ParsedValue value)
            {
                return new ParsedParameters(true, value, Array.Empty<NamedValue>());
            }

            public static ParsedParameters Named(NamedValue[] values)
            {
                return new ParsedParameters(false, default, values);
            }
        }

        private readonly struct ParsedBlockStart
        {
            public readonly string Markup;
            public readonly NamedValue[] Properties;
            public readonly string ScopeText;

            public ParsedBlockStart(string markup, NamedValue[] properties, string scopeText)
            {
                Markup = markup;
                Properties = properties;
                ScopeText = scopeText;
            }
        }

        private readonly struct ParsedSingleMarker
        {
            public readonly string Markup;
            public readonly string Method;
            public readonly ParsedParameters Parameters;
            public readonly string ScopeText;

            public ParsedSingleMarker(string markup, string method, ParsedParameters parameters, string scopeText)
            {
                Markup = markup;
                Method = method;
                Parameters = parameters;
                ScopeText = scopeText;
            }
        }

        private sealed class BlockFrame
        {
            public readonly BlockNode Node;

            public BlockFrame(BlockNode node)
            {
                Node = node;
            }
        }

        private readonly struct SourceSpan
        {
            public readonly int StartIndex;
            public readonly int EndIndex;

            public SourceSpan(int startIndex, int endIndex)
            {
                StartIndex = startIndex;
                EndIndex = endIndex;
            }
        }
    }

    /// <summary>
    /// Separates parsing authoring markup from binding content ranges to the TMP output generated
    /// after TextPipeline marker text has been removed.
    /// </summary>
    public sealed class MarkupParsePlan
    {
        private readonly TMP_CharacterInfo[] expectedFormalCharacters;

        internal MarkupParsePlan(RootNode root, string formalisedText, TMP_CharacterInfo[] expectedFormalCharacters)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            FormalisedText = formalisedText ?? throw new ArgumentNullException(nameof(formalisedText));
            this.expectedFormalCharacters = expectedFormalCharacters ??
                                            throw new ArgumentNullException(nameof(expectedFormalCharacters));
        }

        public RootNode Root { get; }
        public string FormalisedText { get; }

        /// <summary>
        /// Verifies that TMP generated the same formal character sequence observed while planning,
        /// then binds every content leaf to this live TextInfo instance.
        /// </summary>
        public RootNode Bind(TMP_TextInfo formalTextInfo)
        {
            ValidateFormalTextInfo(formalTextInfo);
            BindContentRanges(Root, formalTextInfo);
            return Root;
        }

        private void ValidateFormalTextInfo(TMP_TextInfo formalTextInfo)
        {
            if (formalTextInfo == null) throw new ArgumentNullException(nameof(formalTextInfo));
            if (formalTextInfo.characterInfo == null || formalTextInfo.characterCount < 0 ||
                formalTextInfo.characterCount > formalTextInfo.characterInfo.Length)
                throw new ArgumentException("Formal TMP character information is incomplete.", nameof(formalTextInfo));

            if (formalTextInfo.characterCount != expectedFormalCharacters.Length)
                throw new ArgumentException(
                    "Formal TMP text does not match the character count predicted from the authoring markup.",
                    nameof(formalTextInfo));

            for (int index = 0; index < expectedFormalCharacters.Length; index++)
            {
                TMP_CharacterInfo expected = expectedFormalCharacters[index];
                TMP_CharacterInfo actual = formalTextInfo.characterInfo[index];
                if (actual.character != expected.character || actual.elementType != expected.elementType ||
                    actual.stringLength != expected.stringLength ||
                    actual.spriteAsset != expected.spriteAsset || actual.spriteIndex != expected.spriteIndex)
                    throw new ArgumentException(
                        "Formal TMP text does not match the character sequence predicted from the authoring markup " +
                        $"at index {index}.", nameof(formalTextInfo));
            }
        }

        private static void BindContentRanges(BlockNode parent, TMP_TextInfo formalTextInfo)
        {
            foreach (Node node in parent.Children)
            {
                switch (node)
                {
                    case ContentNode content:
                        content.Content = new TextInfoRange(formalTextInfo, content.StartIndex, content.EndIndex);
                        break;
                    case BlockNode block:
                        BindContentRanges(block, formalTextInfo);
                        break;
                }
            }
        }
    }

    /// <summary>Diagnostic emitted while a markup document is recovered instead of aborted.</summary>
    public sealed class MarkupParseDiagnostic
    {
        public MarkupParseErrorKind Kind { get; }
        public int StartIndex { get; }
        public int EndIndex { get; }
        public string Message { get; }

        internal MarkupParseDiagnostic(MarkupParseErrorKind kind, int startIndex, int endIndex, string message)
        {
            Kind = kind;
            StartIndex = startIndex;
            EndIndex = endIndex;
            Message = message;
        }
    }

    public enum MarkupParseErrorKind
    {
        Lexical,
        Syntax,
        Contextual,
        Contract
    }
}
