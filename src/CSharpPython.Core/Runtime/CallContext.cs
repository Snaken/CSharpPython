namespace CSharpPython
{
    /// <summary>
    /// Passed to every native function. It describes the keyword arguments of the current call and, for host
    /// functions, gives access to the running task (to request waits and so on).
    /// </summary>
    public sealed partial class CallContext
    {
        private string[] _keywordNames = System.Array.Empty<string>();

        /// <summary>Where <c>print</c> sends its text. Set by the VM; null discards output.</summary>
        internal System.Action<string> OutputSink;
        private int _positionalCount;

        internal void SetCall(int positionalCount, string[] keywordNames)
        {
            _positionalCount = positionalCount;
            _keywordNames = keywordNames ?? System.Array.Empty<string>();
        }

        /// <summary>Writes text to the script's output, exactly as Python would write it to stdout.</summary>
        public void Write(string text) => OutputSink?.Invoke(text);

        /// <summary>The number of positional arguments; keyword values follow them in the argument span.</summary>
        public int PositionalCount => _positionalCount;

        /// <summary>The names of the keyword arguments, in the order their values appear after the positional ones.</summary>
        public string[] KeywordNames => _keywordNames;

        /// <summary>Looks up a keyword argument by name.</summary>
        public bool TryGetKeyword(System.ReadOnlySpan<Value> args, string name, out Value value)
        {
            for (int i = 0; i < _keywordNames.Length; i++)
            {
                if (_keywordNames[i] == name)
                {
                    value = args[_positionalCount + i];
                    return true;
                }
            }
            value = default;
            return false;
        }
    }
}
