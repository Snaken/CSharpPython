using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython
{
    internal static partial class Ops
    {
        public static bool IsCounter(Value v) => v.Kind == ValueKind.Dict && ((PyDict)v.RawObject).Flavor == DictFlavor.Counter;

        /// <summary>What <c>d[key]</c> gives for a key a dictionary does not have, or throws the KeyError a plain dictionary throws.</summary>
        private static Value MissingKey(PyDict dict, Value key)
        {
            switch (dict.Flavor)
            {
                case DictFlavor.Counter:
                    return Value.FromInt(0);
                case DictFlavor.Default:
                {
                    if (dict.Factory.IsNone) throw new PyError("KeyError", Repr(key));
                    var task = PyTask.Current;
                    if (task == null) throw new InvalidOperationException("A defaultdict can only make a missing value while a script is running.");
                    Value made = task.CallSync(dict.Factory, Array.Empty<Value>());
                    dict.Set(key, made);
                    return made;
                }
                default:
                    throw new PyError("KeyError", Repr(key));
            }
        }

        /// <summary>The items of a counter, the largest count first; items with equal counts keep their order of insertion.</summary>
        internal static List<KeyValuePair<Value, Value>> MostCommon(PyDict dict)
        {
            var items = dict.Pairs().ToList();
            var comparer = Comparer<Value>.Create((x, y) => Compare(x, y, Order.Gt) ? -1 : Compare(y, x, Order.Gt) ? 1 : 0);
            return items.OrderBy(kv => kv.Value, comparer).ToList();
        }

        // Counters compare as if a missing count were zero.
        private static bool CounterEqual(PyDict x, PyDict y, int depth)
        {
            var zero = Value.FromInt(0);
            foreach (var kv in x.Pairs())
                if (!Equal(kv.Value, y.TryGetValue(kv.Key, out Value other) ? other : zero, depth + 1)) return false;
            foreach (var kv in y.Pairs())
                if (!x.ContainsKey(kv.Key) && !Equal(kv.Value, zero, depth + 1)) return false;
            return true;
        }

        /// <summary><c>+</c> (sum), <c>-</c> (difference), <c>|</c> (maximum) and <c>&amp;</c> (minimum) of two counters, keeping only positive counts.</summary>
        internal static Value CounterArithmetic(Value a, Value b, char op)
        {
            var x = (PyDict)a.RawObject;
            var y = (PyDict)b.RawObject;
            var result = new PyDict { Flavor = DictFlavor.Counter };
            var zero = Value.FromInt(0);
            void Keep(Value key, Value count)
            {
                if (Compare(count, zero, Order.Gt)) result.Set(key, count);
            }
            foreach (var kv in x.Pairs())
            {
                Value other = y.TryGetValue(kv.Key, out Value found) ? found : zero;
                switch (op)
                {
                    case '+': Keep(kv.Key, Add(kv.Value, other)); break;
                    case '-': Keep(kv.Key, Sub(kv.Value, other)); break;
                    case '|': Keep(kv.Key, Compare(kv.Value, other, Order.Ge) ? kv.Value : other); break;
                    default: Keep(kv.Key, Compare(kv.Value, other, Order.Le) ? kv.Value : other); break;
                }
            }
            foreach (var kv in y.Pairs())
            {
                if (x.ContainsKey(kv.Key)) continue;
                switch (op)
                {
                    case '+': Keep(kv.Key, kv.Value); break;
                    case '-': Keep(kv.Key, Sub(zero, kv.Value)); break;
                    case '|': Keep(kv.Key, kv.Value); break;
                    default: break;   // the minimum with a missing count is never positive
                }
            }
            return Value.FromDict(result);
        }
    }
}
