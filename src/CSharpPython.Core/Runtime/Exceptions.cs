using System.Collections.Generic;

namespace CSharpPython
{
    /// <summary>A Python exception class such as <c>ValueError</c>. Types form a small hierarchy rooted at <c>Exception</c>.</summary>
    public sealed class PyExceptionType
    {
        public readonly string Name;
        public readonly PyExceptionType Parent;

        internal PyExceptionType(string name, PyExceptionType parent)
        {
            Name = name;
            Parent = parent;
        }

        /// <summary>True if this type is <paramref name="other"/> or derives from it.</summary>
        public bool IsSubclassOf(PyExceptionType other)
        {
            for (var t = this; t != null; t = t.Parent)
                if (t == other) return true;
            return false;
        }

        public override string ToString() => Name;
    }

    /// <summary>The built-in exception types.</summary>
    public static class ExceptionTypes
    {
        private static readonly Dictionary<string, PyExceptionType> ByName = new Dictionary<string, PyExceptionType>();

        public static readonly PyExceptionType BaseException = Define("BaseException", null);
        public static readonly PyExceptionType Exception = Define("Exception", BaseException);
        public static readonly PyExceptionType SystemExit = Define("SystemExit", BaseException);
        public static readonly PyExceptionType KeyboardInterrupt = Define("KeyboardInterrupt", BaseException);
        public static readonly PyExceptionType GeneratorExit = Define("GeneratorExit", BaseException);
        public static readonly PyExceptionType AssertionError = Define("AssertionError", Exception);
        public static readonly PyExceptionType ArithmeticError = Define("ArithmeticError", Exception);
        public static readonly PyExceptionType ZeroDivisionError = Define("ZeroDivisionError", ArithmeticError);
        public static readonly PyExceptionType OverflowError = Define("OverflowError", ArithmeticError);
        public static readonly PyExceptionType LookupError = Define("LookupError", Exception);
        public static readonly PyExceptionType IndexError = Define("IndexError", LookupError);
        public static readonly PyExceptionType KeyError = Define("KeyError", LookupError);
        public static readonly PyExceptionType ValueError = Define("ValueError", Exception);
        public static readonly PyExceptionType TypeError = Define("TypeError", Exception);
        public static readonly PyExceptionType NameError = Define("NameError", Exception);
        public static readonly PyExceptionType UnboundLocalError = Define("UnboundLocalError", NameError);
        public static readonly PyExceptionType AttributeError = Define("AttributeError", Exception);
        public static readonly PyExceptionType ImportError = Define("ImportError", Exception);
        public static readonly PyExceptionType RuntimeError = Define("RuntimeError", Exception);
        public static readonly PyExceptionType RecursionError = Define("RecursionError", RuntimeError);
        public static readonly PyExceptionType NotImplementedError = Define("NotImplementedError", RuntimeError);
        public static readonly PyExceptionType StopIteration = Define("StopIteration", Exception);
        public static readonly PyExceptionType MemoryError = Define("MemoryError", Exception);
        public static readonly PyExceptionType FloatingPointError = Define("FloatingPointError", ArithmeticError);
        public static readonly PyExceptionType ModuleNotFoundError = Define("ModuleNotFoundError", ImportError);
        public static readonly PyExceptionType EOFError = Define("EOFError", Exception);
        public static readonly PyExceptionType OSError = Define("OSError", Exception);
        public static readonly PyExceptionType FileNotFoundError = Define("FileNotFoundError", OSError);
        public static readonly PyExceptionType FileExistsError = Define("FileExistsError", OSError);
        public static readonly PyExceptionType PermissionError = Define("PermissionError", OSError);
        public static readonly PyExceptionType TimeoutError = Define("TimeoutError", OSError);
        public static readonly PyExceptionType UnicodeError = Define("UnicodeError", ValueError);
        public static readonly PyExceptionType SyntaxError = Define("SyntaxError", Exception);
        public static readonly PyExceptionType IndentationError = Define("IndentationError", SyntaxError);
        public static readonly PyExceptionType TabError = Define("TabError", IndentationError);
        public static readonly PyExceptionType SystemError = Define("SystemError", Exception);
        public static readonly PyExceptionType ReferenceError = Define("ReferenceError", Exception);
        public static readonly PyExceptionType BufferError = Define("BufferError", Exception);
        public static readonly PyExceptionType StopAsyncIteration = Define("StopAsyncIteration", Exception);
        public static readonly PyExceptionType Warning = Define("Warning", Exception);
        public static readonly PyExceptionType UserWarning = Define("UserWarning", Warning);
        public static readonly PyExceptionType DeprecationWarning = Define("DeprecationWarning", Warning);
        public static readonly PyExceptionType PendingDeprecationWarning = Define("PendingDeprecationWarning", Warning);
        public static readonly PyExceptionType RuntimeWarning = Define("RuntimeWarning", Warning);
        public static readonly PyExceptionType SyntaxWarning = Define("SyntaxWarning", Warning);
        public static readonly PyExceptionType FutureWarning = Define("FutureWarning", Warning);
        public static readonly PyExceptionType ImportWarning = Define("ImportWarning", Warning);
        public static readonly PyExceptionType UnicodeWarning = Define("UnicodeWarning", Warning);
        public static readonly PyExceptionType ResourceWarning = Define("ResourceWarning", Warning);

        private static PyExceptionType Define(string name, PyExceptionType parent)
        {
            var t = new PyExceptionType(name, parent);
            ByName[name] = t;
            return t;
        }

        public static bool TryGet(string name, out PyExceptionType type)
        {
            lock (ByName) return ByName.TryGetValue(name, out type);
        }

        /// <summary>
        /// Looks up a type by name. Unknown names are created on the fly as subclasses of <c>Exception</c>, so a
        /// host can <c>throw new PyError("GameError", ...)</c> without registering anything first.
        /// </summary>
        public static PyExceptionType GetOrCreate(string name)
        {
            lock (ByName)
            {
                if (ByName.TryGetValue(name, out var t)) return t;
                return Define(name, Exception);
            }
        }

        /// <summary>Registers a new exception type that derives from <paramref name="parent"/>.</summary>
        public static PyExceptionType Register(string name, PyExceptionType parent)
        {
            lock (ByName) return Define(name, parent ?? Exception);
        }

        public static IEnumerable<PyExceptionType> All
        {
            get { lock (ByName) return new List<PyExceptionType>(ByName.Values); }
        }
    }

    /// <summary>One entry of a traceback: where a function was executing.</summary>
    public readonly struct TraceFrame
    {
        public readonly string Function;
        public readonly int Line;

        public TraceFrame(string function, int line)
        {
            Function = function;
            Line = line;
        }

        public override string ToString() => "line " + Line + ", in " + Function;
    }

    /// <summary>An exception instance, created by <c>raise</c> or by the VM.</summary>
    public sealed class PyExceptionValue
    {
        public readonly PyExceptionType Type;

        // For an exception made from an instance of a script class, the message is worked out on first use.
        private string _message;
        private readonly PyInstance _instance;

        /// <summary>The instance, when this exception was raised from an instance of a script exception class.</summary>
        internal PyInstance Instance => _instance;

        public string Message
        {
            get
            {
                if (_message == null)
                {
                    try { _message = Ops.Str(Value.FromInstance(_instance)); }
                    catch (System.Exception) { _message = Ops.ExceptionArgsText(_instance); }
                }
                return _message;
            }
        }

        /// <summary>The value a <c>StopIteration</c> carries: what the generator returned. None for other exceptions.</summary>
        public Value Payload;

        /// <summary>The innermost frame first. Filled in when the exception is raised.</summary>
        public List<TraceFrame> Trace = new List<TraceFrame>();

        public PyExceptionValue(PyExceptionType type, string message)
        {
            Type = type;
            _message = message ?? "";
        }

        internal PyExceptionValue(PyInstance instance)
        {
            _instance = instance;
            Type = instance.Class.ExceptionRoot;
            if (instance.Trace != null) Trace = instance.Trace;
        }

        /// <summary>The name of the exception's class: the script class for one raised from an instance.</summary>
        internal string TypeName => _instance != null ? _instance.Class.Name : Type.Name;

        public override string ToString() => Message.Length == 0 ? TypeName : TypeName + ": " + Message;
    }
}
