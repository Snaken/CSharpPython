using CSharpPython.Compilation;

namespace CSharpPython
{
    internal enum GeneratorState { NotStarted, Suspended, Running, Done }

    /// <summary>
    /// A generator: a function body that can be paused at <c>yield</c>. While it is paused, its locals, operand stack
    /// and position are kept here; while it runs, they live on the task's stack like any other frame, which is why a
    /// running generator can wait and a saved task can contain one.
    /// </summary>
    internal sealed class PyGenerator : PyIterator
    {
        public readonly Code Code;
        public readonly Cell[] Closure;
        public readonly PyClass Owner;
        public GeneratorState State;

        /// <summary>The locals followed by the operand stack, as of the last yield (the locals alone before the start).</summary>
        public Value[] Saved;
        public int Pc;

        /// <summary>The open try blocks as handler address and stack height relative to the frame's base.</summary>
        public int[] HandlerAddresses = System.Array.Empty<int>();
        public int[] HandlerHeights = System.Array.Empty<int>();
        public Value ExcInfo;
        public Value ReturnValue;

        public PyGenerator(Code code, Cell[] closure, PyClass owner, Value[] locals)
        {
            Code = code;
            Closure = closure;
            Owner = owner;
            Saved = locals;
            ReturnValue = Value.None;
        }

        public string Name => Code.Name;

        /// <summary>Runs the generator to its next yield from native code. This cannot wait.</summary>
        public bool Step(Value sent, out Value value)
        {
            var task = PyTask.Current ?? throw new PyError("RuntimeError", "a generator can only be run by a script");
            return task.ResumeSync(this, sent, out value);
        }

        public override bool Next(out Value value) => Step(Value.None, out value);

        /// <summary>Ends the generator. Unlike Python, pending <c>finally</c> blocks do not run.</summary>
        public void Close()
        {
            if (State == GeneratorState.Running) throw new PyError("ValueError", "generator already executing");
            State = GeneratorState.Done;
            Saved = null;
        }
    }
}
