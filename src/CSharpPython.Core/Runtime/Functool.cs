using System.Collections.Generic;
using System.Linq;

namespace CSharpPython
{
    internal enum FunctoolMode : byte
    {
        Partial, Cached, CacheMaker, Wraps, ItemGetter, AttrGetter, MethodCaller,
    }

    /// <summary>
    /// A callable made by <c>functools</c> or <c>operator</c>: <c>partial</c>, the wrapper <c>lru_cache</c> and
    /// <c>cache</c> put around a function, and <c>itemgetter</c>, <c>attrgetter</c> and <c>methodcaller</c>.
    /// What it holds depends on <see cref="Mode"/>; the call itself is made by <c>PyTask.CallFunctool</c>.
    /// </summary>
    internal sealed class PyFunctool
    {
        public readonly FunctoolMode Mode;

        /// <summary>Partial and Cached: the function. Wraps: the function it stands for. Otherwise None.</summary>
        public readonly Value Func;

        /// <summary>Partial: the arguments given up front. ItemGetter: the items. AttrGetter and MethodCaller: the name, then the arguments.</summary>
        public readonly Value[] Args;

        /// <summary>Partial: the keyword arguments given up front, by name.</summary>
        public readonly string[] KwNames;

        public readonly Value[] KwValues;

        // Cached and CacheMaker: the size limit (-1 for none), whether argument types count, and the results by argument tuple.
        public readonly long MaxSize;
        public readonly bool Typed;
        public readonly PyDict Cache;
        public long Hits, Misses;

        public PyFunctool(FunctoolMode mode, Value func, Value[] args, string[] kwNames = null, Value[] kwValues = null,
            long maxSize = -1, bool typed = false, PyDict cache = null)
        {
            Mode = mode;
            Func = func;
            Args = args;
            KwNames = kwNames ?? System.Array.Empty<string>();
            KwValues = kwValues ?? System.Array.Empty<Value>();
            MaxSize = maxSize;
            Typed = typed;
            Cache = cache ?? (mode == FunctoolMode.Cached ? new PyDict() : null);
        }

        public string TypeName
        {
            get
            {
                switch (Mode)
                {
                    case FunctoolMode.Partial: return "partial";
                    case FunctoolMode.Cached: return "_lru_cache_wrapper";
                    case FunctoolMode.CacheMaker: return "_lru_cache_decorator";
                    case FunctoolMode.Wraps: return "_wraps_decorator";
                    case FunctoolMode.ItemGetter: return "itemgetter";
                    case FunctoolMode.AttrGetter: return "attrgetter";
                    default: return "methodcaller";
                }
            }
        }

        public string Module => Mode == FunctoolMode.ItemGetter || Mode == FunctoolMode.AttrGetter || Mode == FunctoolMode.MethodCaller ? "operator" : "functools";

        /// <summary>Whether the arrays have the shape the mode needs, which a damaged snapshot might not give them.</summary>
        public bool IsConsistent()
        {
            if (KwNames.Length != KwValues.Length) return false;
            switch (Mode)
            {
                case FunctoolMode.Partial: return Func.Kind != ValueKind.None && Func.Kind != ValueKind.Unbound;
                case FunctoolMode.Cached: return Func.Kind != ValueKind.None && Func.Kind != ValueKind.Unbound && Cache != null && MaxSize >= -1 && Hits >= 0 && Misses >= 0 && Args.Length == 0;
                case FunctoolMode.CacheMaker: return MaxSize >= -1 && Args.Length == 0;
                case FunctoolMode.Wraps: return Args.Length == 0;
                case FunctoolMode.ItemGetter: return Args.Length >= 1;
                case FunctoolMode.AttrGetter: return Args.Length >= 1 && Args.All(a => a.Kind == ValueKind.Str);
                case FunctoolMode.MethodCaller: return Args.Length >= 1 && Args[0].Kind == ValueKind.Str;
                default: return false;
            }
        }

        /// <summary>The key a call is cached under: the arguments, then the keyword arguments by name, then their types if those count.</summary>
        public Value CacheKey(Value[] positional, string[] kwNames, Value[] kwValues)
        {
            // Like CPython, one plain int or str stands for itself, so f(1) and f(1.0) are cached apart (as are f(1) and f((1,))).
            if (!Typed && kwNames.Length == 0 && positional.Length == 1 && (positional[0].Kind == ValueKind.Int || positional[0].Kind == ValueKind.Str))
                return positional[0];
            var kw = new List<Value>();
            for (int i = 0; i < kwNames.Length; i++)
            {
                kw.Add(Value.FromString(kwNames[i]));
                kw.Add(kwValues[i]);
            }
            Value types = Value.Tuple();
            if (Typed)
            {
                types = Value.Tuple(positional.Concat(kwValues).Select(v => Value.FromString(v.TypeName)).ToArray());
            }
            return Value.Tuple(Value.Tuple(positional), Value.Tuple(kw.ToArray()), types);
        }

        /// <summary>Looks a call up in the cache. A hit moves it to the end when the cache has a limit, as the least recently used goes first.</summary>
        public bool TryHit(Value key, out Value result)
        {
            if (MaxSize == 0)
            {
                Misses++;
                result = default;
                return false;
            }
            if (Cache.TryGetValue(key, out result))
            {
                Hits++;
                if (MaxSize > 0)
                {
                    Cache.Remove(key);
                    Cache.Set(key, result);
                }
                return true;
            }
            Misses++;
            return false;
        }

        public void Store(Value key, Value result)
        {
            if (MaxSize == 0) return;
            Cache.Set(key, result);
            if (MaxSize > 0 && Cache.Count > MaxSize)
                foreach (var oldest in Cache.Pairs())
                {
                    Cache.Remove(oldest.Key);
                    break;
                }
        }

        /// <summary>Runs <c>itemgetter</c> and <c>attrgetter</c> on a value, which needs no frame. False for the other modes.</summary>
        public bool TryGet(Value obj, out Value result)
        {
            result = default;
            if (Mode == FunctoolMode.ItemGetter)
            {
                if (Args.Length == 1) result = Ops.GetItem(obj, Args[0]);
                else result = Value.Tuple(Args.Select(item => Ops.GetItem(obj, item)).ToArray());
                return true;
            }
            if (Mode == FunctoolMode.AttrGetter)
            {
                if (Args.Length == 1) result = Attribute(obj, (string)Args[0].RawObject);
                else result = Value.Tuple(Args.Select(name => Attribute(obj, (string)name.RawObject)).ToArray());
                return true;
            }
            return false;
        }

        // A dotted name such as "pos.x" follows one attribute after the other.
        private static Value Attribute(Value obj, string dotted)
        {
            foreach (var part in dotted.Split('.')) obj = Ops.GetAttr(obj, part);
            return obj;
        }

        public void ClearCache()
        {
            foreach (var key in Cache.Pairs().Select(p => p.Key).ToList()) Cache.Remove(key);
            Hits = Misses = 0;
        }
    }
}
