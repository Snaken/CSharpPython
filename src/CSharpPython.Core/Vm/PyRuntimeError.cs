using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpPython
{
    /// <summary>
    /// A script stopped because of an unhandled error. It carries the Python exception type, the message and a
    /// traceback with line numbers.
    /// </summary>
    public sealed class PyRuntimeError : Exception
    {
        public string TypeName { get; }
        public string Problem { get; }
        public string SourceName { get; }

        /// <summary>The traceback, innermost frame first.</summary>
        public IReadOnlyList<TraceFrame> Trace { get; }

        /// <summary>The error given by <c>raise ... from</c> as this one's cause (also its <see cref="Exception.InnerException"/>), or null.</summary>
        public PyRuntimeError Cause { get; internal set; }

        /// <summary>The error that was being handled when this one was raised, unless it has a <see cref="Cause"/> or <c>from None</c> hides it; or null.</summary>
        public PyRuntimeError Context { get; internal set; }

        /// <summary>The line the error happened on, or 0 if unknown.</summary>
        public int Line => Trace.Count > 0 ? Trace[0].Line : 0;

        internal PyRuntimeError(string typeName, string problem, string sourceName, IReadOnlyList<TraceFrame> trace, Exception inner = null)
            : base(Describe(typeName, problem, trace), inner)
        {
            TypeName = typeName;
            Problem = problem;
            SourceName = sourceName;
            Trace = trace;
        }

        private static string Describe(string typeName, string problem, IReadOnlyList<TraceFrame> trace)
        {
            string text = problem.Length == 0 ? typeName : typeName + ": " + problem;
            return trace.Count > 0 ? text + " (line " + trace[0].Line + ")" : text;
        }

        /// <summary>
        /// A Python-style traceback, outermost call first, ending with the error itself. An error with a cause or a
        /// context is preceded by theirs, as in CPython.
        /// </summary>
        public string FormatTraceback()
        {
            var sb = new StringBuilder();
            AppendTraceback(sb);
            return sb.ToString();
        }

        private void AppendTraceback(StringBuilder sb)
        {
            if (Cause != null)
            {
                Cause.AppendTraceback(sb);
                sb.Append("\n\nThe above exception was the direct cause of the following exception:\n\n");
            }
            else if (Context != null)
            {
                Context.AppendTraceback(sb);
                sb.Append("\n\nDuring handling of the above exception, another exception occurred:\n\n");
            }
            sb.Append("Traceback (most recent call last):\n");
            for (int i = Trace.Count - 1; i >= 0; i--)
                sb.Append("  File \"").Append(SourceName).Append("\", ").Append(Trace[i]).Append('\n');
            sb.Append(Problem.Length == 0 ? TypeName : TypeName + ": " + Problem);
        }
    }
}
