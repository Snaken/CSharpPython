using CSharpPython.Compilation;

namespace CSharpPython
{
    /// <summary>A compiled script. It is immutable, so one script can be started as many tasks.</summary>
    public sealed class Script
    {
        internal readonly Code Code;

        internal Script(Code code)
        {
            Code = code;
        }

        public string Name => Code.SourceName;

        /// <summary>A readable bytecode listing, for debugging.</summary>
        public string Disassemble() => Code.Disassemble();
    }
}
