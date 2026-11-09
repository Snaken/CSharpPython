using System;
using System.Collections.Generic;
using CSharpPython.Syntax;

namespace CSharpPython.Editor
{
    /// <summary>
    /// What an editor needs to draw a script: its coloured spans with the problems already marked on them, and the
    /// problems themselves. <see cref="Build"/> lexes the text once for both, where calling
    /// <see cref="SyntaxHighlighter.Highlight(string, PyVm)"/> and <see cref="Analyzer.Analyze(string)"/> separately lexes it up to three times.
    /// </summary>
    public sealed class EditorView
    {
        /// <summary>
        /// The spans of <see cref="SyntaxHighlighter.Highlight(string, PyVm)"/>, split wherever a problem starts or ends, so that
        /// each one lies wholly inside or wholly outside every problem. A span inside a problem has
        /// <see cref="HighlightSpan.Problem"/> set to its message. A problem that lies only in whitespace (such as a
        /// bad indent) marks no span; it is in <see cref="Diagnostics"/>. Sorted, not overlapping, whitespace not covered.
        /// </summary>
        public IReadOnlyList<HighlightSpan> Spans { get; }

        /// <summary>What <see cref="Analyzer.Analyze(string)"/> returns for the same text: empty when the script compiles.</summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        private EditorView(List<HighlightSpan> spans, List<Diagnostic> diagnostics)
        {
            Spans = spans;
            Diagnostics = diagnostics;
        }

        /// <summary>
        /// Highlights and analyzes <paramref name="source"/> in one pass. <paramref name="vm"/> decides which names
        /// are built-ins (a default VM when null). Never throws.
        /// </summary>
        public static EditorView Build(string source, PyVm vm = null)
        {
            source = source ?? "";
            var tokens = Lexer.TokenizeTolerant(source, out var lexical);
            var spans = SyntaxHighlighter.Highlight(source, tokens, vm);
            var diagnostics = Analyzer.Analyze(tokens, lexical);
            return new EditorView(Mark(source, spans, diagnostics), diagnostics);
        }

        private readonly struct Range
        {
            public readonly int Start;
            public readonly int End;
            public readonly string Message;

            public Range(int start, int end, string message)
            {
                Start = start;
                End = end;
                Message = message;
            }
        }

        private static List<HighlightSpan> Mark(string source, List<HighlightSpan> spans, List<Diagnostic> diagnostics)
        {
            if (diagnostics.Count == 0) return spans;
            var ranges = new List<Range>();
            foreach (var d in diagnostics)
            {
                int start = d.Offset >= 0 ? d.Offset : OffsetOf(source, d.Line, d.Column);
                start = Math.Max(0, Math.Min(start, source.Length));
                int end = Math.Min(source.Length, start + Math.Max(d.Length, 1)); // a problem that points at a place marks one character
                if (end > start) ranges.Add(new Range(start, end, d.Message));
            }
            ranges.Sort((a, b) => a.Start.CompareTo(b.Start));

            var result = new List<HighlightSpan>(spans.Count + ranges.Count * 2);
            int first = 0; // ranges before it end before the current span starts
            foreach (var span in spans)
            {
                while (first < ranges.Count && ranges[first].End <= span.Start) first++;
                int cursor = span.Start;
                for (int j = first; j < ranges.Count && ranges[j].Start < span.End; j++)
                {
                    int from = Math.Max(ranges[j].Start, cursor);
                    int to = Math.Min(ranges[j].End, span.End);
                    if (to <= from) continue; // covered by an earlier, overlapping problem
                    if (from > cursor) result.Add(new HighlightSpan(cursor, from - cursor, span.Kind));
                    result.Add(new HighlightSpan(from, to - from, span.Kind, ranges[j].Message));
                    cursor = to;
                }
                if (cursor < span.End) result.Add(new HighlightSpan(cursor, span.End - cursor, span.Kind));
            }
            return result;
        }

        // A problem found while parsing an embedded expression carries no offset, only a line and a column (both from 1).
        private static int OffsetOf(string source, int line, int column)
        {
            int offset = 0;
            for (int l = 1; l < line; l++)
            {
                int next = source.IndexOf('\n', offset);
                if (next < 0) return source.Length;
                offset = next + 1;
            }
            return Math.Min(source.Length, offset + Math.Max(column - 1, 0));
        }
    }
}
