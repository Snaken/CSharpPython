using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython
{
    public sealed partial class PyTask
    {
        // Calls a callable made by functools or operator (see PyFunctool).
        private int CallFunctool(Value callee, int calleeIndex, int argStart, int argc, int positional, string[] kwNames)
        {
            var f = (PyFunctool)callee.RawObject;
            switch (f.Mode)
            {
                case FunctoolMode.Partial: return CallPartial(f, calleeIndex, argStart, positional, kwNames);
                case FunctoolMode.Cached: return CallCached(f, calleeIndex, argStart, argc, positional, kwNames);
            }
            if (kwNames != null) throw new PyError("TypeError", f.TypeName + "() takes no keyword arguments");
            if (argc != 1) throw new PyError("TypeError", f.TypeName + " expected 1 argument, got " + argc);
            Value arg = _stack[argStart];
            Value result;
            switch (f.Mode)
            {
                case FunctoolMode.CacheMaker:
                    if (!PyTypes.IsCallable(arg)) throw new PyError("TypeError", "the first argument must be callable");
                    result = Value.FromFunctool(new PyFunctool(FunctoolMode.Cached, arg, Array.Empty<Value>(), maxSize: f.MaxSize, typed: f.Typed));
                    break;
                case FunctoolMode.Wraps:
                    result = arg;
                    break;
                case FunctoolMode.CmpKey:
                    result = Value.FromFunctool(new PyFunctool(FunctoolMode.CmpKey, f.Func, new[] { arg }));
                    break;
                case FunctoolMode.MethodCaller:
                    result = CallSync(Ops.GetAttr(arg, (string)f.Args[0].RawObject), f.Args.Skip(1).ToArray());
                    break;
                default:
                    f.TryGet(arg, out result);
                    break;
            }
            _sp = calleeIndex;
            Push(result);
            return CallDone;
        }

        // partial: the arguments given up front go first, and keywords given again replace the earlier ones.
        private int CallPartial(PyFunctool f, int calleeIndex, int argStart, int positional, string[] kwNames)
        {
            var names = new List<string>(f.KwNames);
            var values = new List<Value>(f.KwValues);
            int given = kwNames?.Length ?? 0;
            for (int i = 0; i < given; i++)
            {
                Value value = _stack[argStart + positional + i];
                int at = names.IndexOf(kwNames[i]);
                if (at >= 0) values[at] = value;
                else
                {
                    names.Add(kwNames[i]);
                    values.Add(value);
                }
            }
            var callPositional = new Value[positional];
            Array.Copy(_stack, argStart, callPositional, 0, positional);

            int total = f.Args.Length + positional + names.Count;
            EnsureStack(calleeIndex + 1 + total + 8);
            _stack[calleeIndex] = f.Func;
            int at2 = calleeIndex + 1;
            foreach (var a in f.Args) _stack[at2++] = a;
            foreach (var a in callPositional) _stack[at2++] = a;
            foreach (var v in values) _stack[at2++] = v;
            _sp = at2;
            return DispatchCall(f.Func, calleeIndex, calleeIndex + 1, total, f.Args.Length + positional, names.Count > 0 ? names.ToArray() : null);
        }

        // lru_cache and cache: a call seen before answers from the cache; a new one runs the function, and the result is
        // stored when the function returns (see CacheOwner in the Return instruction), however deep the recursion goes.
        private int CallCached(PyFunctool f, int calleeIndex, int argStart, int argc, int positional, string[] kwNames)
        {
            var args = new Value[positional];
            Array.Copy(_stack, argStart, args, 0, positional);
            int given = kwNames?.Length ?? 0;
            var kwValues = new Value[given];
            Array.Copy(_stack, argStart + positional, kwValues, 0, given);
            Value key = f.CacheKey(args, kwNames ?? Array.Empty<string>(), kwValues);
            if (f.TryHit(key, out Value hit))
            {
                _sp = calleeIndex;
                Push(hit);
                return CallDone;
            }
            _stack[calleeIndex] = f.Func;
            int outcome = DispatchCall(f.Func, calleeIndex, argStart, argc, positional, kwNames);
            if (outcome == CallFrame)
            {
                var frame = _frames[_frameCount - 1];
                frame.CacheOwner = f;
                frame.CacheKey = key;
            }
            else if (outcome == CallDone) f.Store(key, _stack[_sp - 1]);
            return outcome;
        }
    }
}
