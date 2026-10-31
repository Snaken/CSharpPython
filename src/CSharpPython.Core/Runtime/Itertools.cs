using System;
using System.Collections.Generic;

namespace CSharpPython
{
    internal enum ItertoolsKind : byte
    {
        Count, Cycle, Repeat, Chain, ChainFrom, Islice, Accumulate, Compress, DropWhile, TakeWhile, FilterFalse, StarMap,
        ZipLongest, Pairwise, Batched, Product, Permutations, Combinations, CombinationsWithReplacement, GroupBy, Grouper, Tee,
    }

    /// <summary>
    /// The iterators of the <c>itertools</c> module. They all keep their position in plain arrays (sources, arguments, counters
    /// and a list of items), so a snapshot can save any of them the same way. What each array holds is described at the
    /// method that uses it.
    /// </summary>
    internal sealed class ItertoolsIterator : PyIterator
    {
        public readonly ItertoolsKind Kind;
        public readonly PyIterator[] Sources;
        public readonly Value[] Args;
        public readonly long[] State;
        public readonly List<Value> Items;

        public ItertoolsIterator(ItertoolsKind kind, PyIterator[] sources, Value[] args, long[] state, List<Value> items = null)
        {
            Kind = kind;
            Sources = sources;
            Args = args;
            State = state;
            Items = items ?? new List<Value>();
        }

        public string TypeName
        {
            get
            {
                switch (Kind)
                {
                    case ItertoolsKind.ChainFrom: return "chain";
                    case ItertoolsKind.Grouper: return "_grouper";
                    case ItertoolsKind.DropWhile: return "dropwhile";
                    case ItertoolsKind.TakeWhile: return "takewhile";
                    case ItertoolsKind.FilterFalse: return "filterfalse";
                    case ItertoolsKind.StarMap: return "starmap";
                    case ItertoolsKind.ZipLongest: return "zip_longest";
                    case ItertoolsKind.CombinationsWithReplacement: return "combinations_with_replacement";
                    case ItertoolsKind.GroupBy: return "groupby";
                    default: return Kind.ToString().ToLowerInvariant();
                }
            }
        }

        /// <summary>Whether the arrays have the shape the kind needs, which a damaged snapshot might not give them.</summary>
        public bool IsConsistent()
        {
            int sources = Sources.Length, args = Args.Length, state = State.Length;
            switch (Kind)
            {
                case ItertoolsKind.Count: return sources == 0 && args == 2 && state == 0;
                case ItertoolsKind.Cycle: return sources == 1 && args == 0 && state == 1 && State[0] >= 0 && State[0] <= Items.Count;
                case ItertoolsKind.Repeat: return sources == 0 && args == 1 && state == 1 && State[0] >= -1;
                case ItertoolsKind.Chain: return sources == 1 && state == 1 && State[0] >= 0 && State[0] <= args;
                case ItertoolsKind.ChainFrom: return sources == 2 && args == 0 && state == 0 && Sources[0] != null;
                case ItertoolsKind.Islice:
                    return sources == 1 && args == 0 && state == 4 && State[0] >= 0 && State[1] >= -1 && State[2] >= 1 && State[3] >= 0;
                case ItertoolsKind.Accumulate: return sources == 1 && Sources[0] != null && args == 2 && state == 1 && State[0] >= 0 && State[0] <= 2;
                case ItertoolsKind.Compress: return sources == 2 && Sources[0] != null && Sources[1] != null && args == 0 && state == 0;
                case ItertoolsKind.DropWhile:
                case ItertoolsKind.TakeWhile:
                    return sources == 1 && Sources[0] != null && args == 1 && state == 1;
                case ItertoolsKind.FilterFalse:
                case ItertoolsKind.StarMap:
                    return sources == 1 && Sources[0] != null && args == 1 && state == 0;
                case ItertoolsKind.ZipLongest:
                {
                    if (args != 1 || state != 1 || State[0] < 0 || State[0] > sources) return false;
                    long active = 0;
                    foreach (var s in Sources) if (s != null) active++;
                    return active == State[0];
                }
                case ItertoolsKind.Pairwise: return sources == 1 && Sources[0] != null && args == 1 && state == 1 && State[0] >= 0 && State[0] <= 2;
                case ItertoolsKind.Batched: return sources == 1 && Sources[0] != null && args == 0 && state == 2 && State[0] >= 1;
                case ItertoolsKind.Product: return ProductIsConsistent();
                case ItertoolsKind.Permutations: return PermutationsIsConsistent();
                case ItertoolsKind.Combinations:
                case ItertoolsKind.CombinationsWithReplacement: return CombinationsIsConsistent();
                case ItertoolsKind.GroupBy: return sources == 1 && Sources[0] != null && args == 4 && state == 4;
                case ItertoolsKind.Tee:
                    return sources == 1 && Sources[0] != null && args == 1 && Args[0].Kind == ValueKind.List && state == 2
                        && State[0] >= 0 && State[0] <= ((PyList)Args[0].RawObject).Items.Count && (State[1] == 0 || State[1] == 1);
                case ItertoolsKind.Grouper: return sources == 1 && Sources[0] is ItertoolsIterator parent && parent.Kind == ItertoolsKind.GroupBy && args == 0 && state == 1;
                default: return false;
            }
        }

        public override bool Next(out Value value)
        {
            Ops.CheckStack();
            switch (Kind)
            {
                case ItertoolsKind.Count: return NextCount(out value);
                case ItertoolsKind.Cycle: return NextCycle(out value);
                case ItertoolsKind.Repeat: return NextRepeat(out value);
                case ItertoolsKind.Chain: return NextChain(out value);
                case ItertoolsKind.ChainFrom: return NextChainFrom(out value);
                case ItertoolsKind.Islice: return NextIslice(out value);
                case ItertoolsKind.Accumulate: return NextAccumulate(out value);
                case ItertoolsKind.Compress: return NextCompress(out value);
                case ItertoolsKind.DropWhile: return NextDropWhile(out value);
                case ItertoolsKind.TakeWhile: return NextTakeWhile(out value);
                case ItertoolsKind.FilterFalse: return NextFilterFalse(out value);
                case ItertoolsKind.StarMap: return NextStarMap(out value);
                case ItertoolsKind.ZipLongest: return NextZipLongest(out value);
                case ItertoolsKind.Pairwise: return NextPairwise(out value);
                case ItertoolsKind.Batched: return NextBatched(out value);
                case ItertoolsKind.Product: return NextProduct(out value);
                case ItertoolsKind.Permutations: return NextPermutations(out value);
                case ItertoolsKind.Combinations: return NextCombinations(out value);
                case ItertoolsKind.CombinationsWithReplacement: return NextCombinationsWithReplacement(out value);
                case ItertoolsKind.GroupBy: return NextGroupBy(out value);
                case ItertoolsKind.Tee: return NextTee(out value);
                default: return NextGrouper(out value);
            }
        }

        private static PyTask Task(string what)
        {
            return PyTask.Current ?? throw new PyError("RuntimeError", what + " can only be consumed while a script is running");
        }

        private bool Done(out Value value)
        {
            value = default;
            return false;
        }

        // Args = [next value, step].
        private bool NextCount(out Value value)
        {
            value = Args[0];
            Args[0] = Ops.Add(value, Args[1]);
            return true;
        }

        // Sources = [the iterator, or null once it has run out]; Items = what it gave; State = [where the replay is].
        private bool NextCycle(out Value value)
        {
            if (Sources[0] != null)
            {
                if (Sources[0].Next(out value))
                {
                    Items.Add(value);
                    return true;
                }
                Sources[0] = null;
            }
            if (Items.Count == 0) return Done(out value);
            value = Items[(int)State[0]];
            State[0] = (State[0] + 1) % Items.Count;
            return true;
        }

        // Args = [the object]; State = [how many are left, or -1 for no end].
        private bool NextRepeat(out Value value)
        {
            if (State[0] == 0) return Done(out value);
            if (State[0] > 0) State[0]--;
            value = Args[0];
            return true;
        }

        // Args = the iterables; State = [how many have been started]; Sources = [the one being read].
        private bool NextChain(out Value value)
        {
            while (true)
            {
                if (Sources[0] == null)
                {
                    if (State[0] >= Args.Length) return Done(out value);
                    Sources[0] = Ops.GetIterator(Args[(int)State[0]++]);
                }
                if (Sources[0].Next(out value)) return true;
                Sources[0] = null;
            }
        }

        // Sources = [the iterator of iterables, the one being read].
        private bool NextChainFrom(out Value value)
        {
            while (true)
            {
                if (Sources[1] == null)
                {
                    if (!Sources[0].Next(out Value inner)) return Done(out value);
                    Sources[1] = Ops.GetIterator(inner);
                }
                if (Sources[1].Next(out value)) return true;
                Sources[1] = null;
            }
        }

        // Sources = [the iterator, or null when finished]; State = [the next index to give, the stop (-1 for none), the step, the index reached].
        private bool NextIslice(out Value value)
        {
            value = default;
            var source = Sources[0];
            if (source == null) return false;
            long stop = State[1];
            if (stop != -1 && State[3] >= stop) return Finish();
            while (State[3] < State[0])
            {
                if (!source.Next(out _)) return Finish();
                State[3]++;
            }
            if (stop != -1 && State[3] >= stop) return Finish();
            if (!source.Next(out value)) return Finish();
            State[3]++;
            long old = State[0];
            State[0] += State[2];
            if (State[0] < old || (stop != -1 && State[0] > stop)) State[0] = stop;
            return true;
        }

        // Like CPython, the source is let go once the slice is over; items up to the stop are only read when the next one is asked for.
        private bool Finish()
        {
            Sources[0] = null;
            return false;
        }

        // Sources = [the iterator]; Args = [the function or None, the running total]; State = [0 nothing yet, 1 a total, 2 the first value is still to be given].
        private bool NextAccumulate(out Value value)
        {
            if (State[0] == 2)
            {
                State[0] = 1;
                value = Args[1];
                return true;
            }
            if (!Sources[0].Next(out Value item)) return Done(out value);
            if (State[0] == 0)
            {
                Args[1] = item;
                State[0] = 1;
            }
            else if (Args[0].IsNone) Args[1] = Ops.Add(Args[1], item);
            else Args[1] = Task("accumulate()").CallSync(Args[0], new[] { Args[1], item });
            value = Args[1];
            return true;
        }

        private bool NextCompress(out Value value)
        {
            while (Sources[0].Next(out value))
            {
                if (!Sources[1].Next(out Value selector)) return Done(out value);
                if (selector.IsTruthy) return true;
            }
            return false;
        }

        // Args = [the predicate]; State = [1 while still dropping].
        private bool NextDropWhile(out Value value)
        {
            while (Sources[0].Next(out value))
            {
                if (State[0] == 1)
                {
                    if (Task("dropwhile()").CallSync(Args[0], value).IsTruthy) continue;
                    State[0] = 0;
                }
                return true;
            }
            return false;
        }

        // Args = [the predicate]; State = [1 once it failed].
        private bool NextTakeWhile(out Value value)
        {
            if (State[0] == 1) return Done(out value);
            if (!Sources[0].Next(out value)) return false;
            if (Task("takewhile()").CallSync(Args[0], value).IsTruthy) return true;
            State[0] = 1;
            return Done(out value);
        }

        private bool NextFilterFalse(out Value value)
        {
            while (Sources[0].Next(out value))
            {
                bool truthy = Args[0].IsNone ? value.IsTruthy : Task("filterfalse()").CallSync(Args[0], value).IsTruthy;
                if (!truthy) return true;
            }
            return false;
        }

        private bool NextStarMap(out Value value)
        {
            if (!Sources[0].Next(out Value arguments)) return Done(out value);
            value = Task("starmap()").CallSync(Args[0], Ops.ToList(arguments).ToArray());
            return true;
        }

        // Sources = the iterators (null once one has run out); Args = [the fill value]; State = [how many are still going].
        private bool NextZipLongest(out Value value)
        {
            if (State[0] == 0) return Done(out value);
            var items = new Value[Sources.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (Sources[i] != null && Sources[i].Next(out items[i])) continue;
                if (Sources[i] != null)
                {
                    Sources[i] = null;
                    if (--State[0] == 0) return Done(out value);
                }
                items[i] = Args[0];
            }
            value = Value.Tuple(items);
            return true;
        }

        // The iterators tee makes share one source and one list of what it has given out. Args = [that list];
        // State = [how many of its items this iterator has returned, 1 once the source is used up].
        private bool NextTee(out Value value)
        {
            var seen = ((PyList)Args[0].RawObject).Items;
            if (State[0] < seen.Count)
            {
                value = seen[(int)State[0]++];
                return true;
            }
            value = default;
            if (State[1] == 1) return false;
            if (!Sources[0].Next(out value))
            {
                State[1] = 1;
                return false;
            }
            seen.Add(value);
            State[0]++;
            return true;
        }

        // Args = [the previous value]; State = [0 nothing read yet, 1 reading, 2 finished].
        private bool NextPairwise(out Value value)
        {
            value = default;
            if (State[0] == 2) return false;
            if (State[0] == 0)
            {
                if (!Sources[0].Next(out Args[0]))
                {
                    State[0] = 2;
                    return false;
                }
                State[0] = 1;
            }
            if (!Sources[0].Next(out Value next))
            {
                State[0] = 2;
                return false;
            }
            value = Value.Tuple(Args[0], next);
            Args[0] = next;
            return true;
        }

        // State = [the size of a batch, 1 if a short batch is an error].
        private bool NextBatched(out Value value)
        {
            var batch = new List<Value>();
            while (batch.Count < State[0] && Sources[0].Next(out Value item)) batch.Add(item);
            if (batch.Count == 0) return Done(out value);
            if (State[1] == 1 && batch.Count < State[0]) throw Ops.ValueError("batched(): incomplete batch");
            value = Value.Tuple(batch.ToArray());
            return true;
        }

        // ---- product, permutations and combinations -------------------------------------------------------------------
        // The pool (or pools one after another) is in Items, and the positions are in State.

        // State = [number of pools, started, finished, the length of each pool..., the position in each pool...].
        private bool ProductIsConsistent()
        {
            if (Sources.Length != 0 || Args.Length != 0 || State.Length < 3) return false;
            long pools = State[0];
            if (pools < 0 || State.Length != 3 + 2 * pools || State[1] < 0 || State[1] > 1 || State[2] < 0 || State[2] > 1) return false;
            long total = 0;
            for (int i = 0; i < pools; i++)
            {
                long length = State[3 + i], position = State[3 + pools + i];
                if (length < 0 || position < 0 || (length > 0 && position >= length) || (length == 0 && position != 0)) return false;
                total += length;
            }
            return total == Items.Count;
        }

        private bool NextProduct(out Value value)
        {
            value = default;
            if (State[2] == 1) return false;
            int pools = (int)State[0];
            int lengths = 3, positions = 3 + pools;
            if (State[1] == 0)
            {
                State[1] = 1;
                for (int i = 0; i < pools; i++)
                {
                    if (State[lengths + i] != 0) continue;
                    State[2] = 1;
                    return false;
                }
            }
            else
            {
                int k = pools - 1;
                for (; k >= 0; k--)
                {
                    if (++State[positions + k] < State[lengths + k]) break;
                    State[positions + k] = 0;
                }
                if (k < 0)
                {
                    State[2] = 1;
                    return false;
                }
            }
            var result = new Value[pools];
            int start = 0;
            for (int i = 0; i < pools; i++)
            {
                result[i] = Items[start + (int)State[positions + i]];
                start += (int)State[lengths + i];
            }
            if (pools == 0) State[2] = 1;
            value = Value.Tuple(result);
            return true;
        }

        // State = [n, r, started, finished, indices (n), cycles (r)].
        private bool PermutationsIsConsistent()
        {
            if (Sources.Length != 0 || Args.Length != 0 || State.Length < 4) return false;
            long n = State[0], r = State[1];
            if (n != Items.Count || r < 0 || r > n || State.Length != 4 + n + r || State[2] < 0 || State[2] > 1 || State[3] < 0 || State[3] > 1) return false;
            var seen = new bool[n];
            for (int i = 0; i < n; i++)
            {
                long index = State[4 + i];
                if (index < 0 || index >= n || seen[index]) return false;
                seen[index] = true;
            }
            for (int i = 0; i < r; i++) if (State[4 + n + i] < 1 || State[4 + n + i] > n - i) return false;
            return true;
        }

        private bool NextPermutations(out Value value)
        {
            value = default;
            if (State[3] == 1) return false;
            int n = (int)State[0], r = (int)State[1];
            int indices = 4, cycles = 4 + n;
            if (State[2] == 0) State[2] = 1;
            else
            {
                int i = r - 1;
                for (; i >= 0; i--)
                {
                    State[cycles + i]--;
                    if (State[cycles + i] == 0)
                    {
                        long moved = State[indices + i];
                        for (int j = i; j < n - 1; j++) State[indices + j] = State[indices + j + 1];
                        State[indices + n - 1] = moved;
                        State[cycles + i] = n - i;
                    }
                    else
                    {
                        int j = (int)State[cycles + i];
                        long swap = State[indices + i];
                        State[indices + i] = State[indices + n - j];
                        State[indices + n - j] = swap;
                        break;
                    }
                }
                if (i < 0)
                {
                    State[3] = 1;
                    return false;
                }
            }
            var result = new Value[r];
            for (int i = 0; i < r; i++) result[i] = Items[(int)State[indices + i]];
            value = Value.Tuple(result);
            return true;
        }

        // State = [n, r, started, finished, indices (r)].
        private bool CombinationsIsConsistent()
        {
            if (Sources.Length != 0 || Args.Length != 0 || State.Length < 4) return false;
            long n = State[0], r = State[1];
            if (n != Items.Count || r < 0 || State.Length != 4 + r || State[2] < 0 || State[2] > 1 || State[3] < 0 || State[3] > 1) return false;
            bool repeats = Kind == ItertoolsKind.CombinationsWithReplacement;
            if (!repeats && r > n) return State[3] == 1 && State[2] == 0;
            for (int i = 0; i < r; i++)
            {
                long index = State[4 + i];
                if (index < 0 || (n > 0 && index >= n) || (n == 0 && index != 0)) return false;
            }
            return true;
        }

        private bool NextCombinations(out Value value)
        {
            value = default;
            if (State[3] == 1) return false;
            int n = (int)State[0], r = (int)State[1];
            int indices = 4;
            if (State[2] == 0) State[2] = 1;
            else
            {
                int i = r - 1;
                for (; i >= 0; i--) if (State[indices + i] != i + n - r) break;
                if (i < 0)
                {
                    State[3] = 1;
                    return false;
                }
                State[indices + i]++;
                for (int j = i + 1; j < r; j++) State[indices + j] = State[indices + j - 1] + 1;
            }
            return CombinationOf(r, out value);
        }

        private bool NextCombinationsWithReplacement(out Value value)
        {
            value = default;
            if (State[3] == 1) return false;
            int n = (int)State[0], r = (int)State[1];
            int indices = 4;
            if (State[2] == 0) State[2] = 1;
            else
            {
                int i = r - 1;
                for (; i >= 0; i--) if (State[indices + i] != n - 1) break;
                if (i < 0)
                {
                    State[3] = 1;
                    return false;
                }
                long next = State[indices + i] + 1;
                for (int j = i; j < r; j++) State[indices + j] = next;
            }
            return CombinationOf(r, out value);
        }

        private bool CombinationOf(int r, out Value value)
        {
            var result = new Value[r];
            for (int i = 0; i < r; i++) result[i] = Items[(int)State[4 + i]];
            value = Value.Tuple(result);
            return true;
        }

        // ---- groupby --------------------------------------------------------------------------------------------------
        // A groupby has Args = [the key function or None, the current key, the current value, the key being grouped] and
        // State = [has a current key, has a key being grouped, has a current value, the number of the latest group].
        // Its groups are Grouper iterators that share it, and State = [their number].

        private bool Step()
        {
            if (!Sources[0].Next(out Value item)) return false;
            Value key = Args[0].IsNone ? item : Task("groupby()").CallSync(Args[0], item);
            Args[2] = item;
            Args[1] = key;
            State[0] = 1;
            State[2] = 1;
            return true;
        }

        private bool NextGroupBy(out Value value)
        {
            State[3]++;
            while (true)
            {
                if (State[0] != 0)
                {
                    if (State[1] == 0) break;
                    if (!Ops.Equal(Args[3], Args[1])) break;
                }
                if (!Step()) return Done(out value);
            }
            Args[3] = Args[1];
            State[1] = 1;
            var group = new ItertoolsIterator(ItertoolsKind.Grouper, new PyIterator[] { this }, new Value[0], new[] { State[3] });
            value = Value.Tuple(Args[1], Value.FromIterator(group));
            return true;
        }

        private bool NextGrouper(out Value value)
        {
            var parent = (ItertoolsIterator)Sources[0];
            if (parent.State[3] != State[0]) return Done(out value);
            if (parent.State[2] == 0 && !parent.Step()) return Done(out value);
            if (!Ops.Equal(parent.Args[3], parent.Args[1])) return Done(out value);
            value = parent.Args[2];
            parent.Args[2] = default;
            parent.State[2] = 0;
            return true;
        }
    }
}
