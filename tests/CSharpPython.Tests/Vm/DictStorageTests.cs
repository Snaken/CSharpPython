using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpPython.Tests.Vm
{
    // The compact storage behind dict and set: positions in a table, no value slot for a set, tombstones and compaction.
    public class DictStorageTests
    {
        private static Value Key(long i) => i % 3 == 0 ? Value.FromString("k" + i) : Value.FromInt(i);

        [Test]
        public void RandomOperationsAgreeWithAnOrdinaryDictionary()
        {
            var random = new Random(12345);
            var dict = new PyDict();
            var model = new Dictionary<string, long>();
            var order = new List<string>();
            for (int step = 0; step < 60000; step++)
            {
                long k = random.Next(0, 700);
                var key = Key(k);
                string name = key.Kind == ValueKind.Str ? (string)key.RawObject : "#" + k;
                switch (random.Next(4))
                {
                    case 0:
                    case 1:
                        dict.Set(key, Value.FromInt(step));
                        if (!model.ContainsKey(name)) order.Add(name);
                        model[name] = step;
                        break;
                    case 2:
                        Assert.That(dict.Remove(key), Is.EqualTo(model.Remove(name)));
                        order.Remove(name);
                        break;
                    default:
                        bool found = dict.TryGetValue(key, out var v);
                        Assert.That(found, Is.EqualTo(model.ContainsKey(name)));
                        if (found) Assert.That(v.AsInt(), Is.EqualTo(model[name]));
                        break;
                }
                Assert.That(dict.Count, Is.EqualTo(model.Count));
            }
            var names = dict.Pairs().Select(p => p.Key.Kind == ValueKind.Str ? (string)p.Key.RawObject : "#" + p.Key.AsInt()).ToList();
            Assert.That(names, Is.EqualTo(order), "insertion order is kept through removals and compaction");
            foreach (var pair in dict.Pairs())
                Assert.That(pair.Value.AsInt(), Is.EqualTo(model[pair.Key.Kind == ValueKind.Str ? (string)pair.Key.RawObject : "#" + pair.Key.AsInt()]));
        }

        [Test]
        public void ASetKeepsNoValues()
        {
            var set = new PySet();
            for (int i = 0; i < 1000; i++) set.Add(Value.FromInt(i));
            Assert.That(set.Count, Is.EqualTo(1000));
            Assert.That(set.Contains(Value.FromInt(999)), Is.True);
            Assert.That(set.Remove(Value.FromInt(5)), Is.True);
            Assert.That(set.Contains(Value.FromInt(5)), Is.False);
            Assert.That(set.Members().Count(), Is.EqualTo(999));
        }

        [Test]
        public void ADictionaryStartingWithNoneValuesGainsRealOnesLater()
        {
            var dict = new PyDict();
            for (int i = 0; i < 20; i++) dict.Set(Value.FromInt(i), Value.None);
            dict.Set(Value.FromInt(7), Value.FromInt(70));
            dict.Set(Value.FromInt(30), Value.FromInt(300));
            Assert.That(dict.TryGetValue(Value.FromInt(7), out var seven) && seven.AsInt() == 70, Is.True);
            Assert.That(dict.TryGetValue(Value.FromInt(8), out var eight) && eight.Kind == ValueKind.None, Is.True);
            Assert.That(dict.TryGetValue(Value.FromInt(30), out var thirty) && thirty.AsInt() == 300, Is.True);
            dict.Set(Value.FromInt(7), Value.None);
            Assert.That(dict.TryGetValue(Value.FromInt(7), out var cleared) && cleared.Kind == ValueKind.None, Is.True);
        }

        [Test]
        public void EqualNumbersAreTheSameKey()
        {
            var dict = new PyDict();
            dict.Set(Value.FromInt(1), Value.FromInt(10));
            for (int i = 2; i < 20; i++) dict.Set(Value.FromInt(i), Value.FromInt(i));
            Assert.That(dict.TryGetValue(Value.FromFloat(1.0), out var viaFloat) && viaFloat.AsInt() == 10, Is.True);
            Assert.That(dict.TryGetValue(Value.True, out var viaBool) && viaBool.AsInt() == 10, Is.True);
            dict.Set(Value.FromFloat(2.0), Value.FromInt(-2));
            Assert.That(dict.Count, Is.EqualTo(19));
        }

        [Test]
        public void ANewKeyCanBeAddedAfterManyRemovals()
        {
            var dict = new PyDict();
            for (int round = 0; round < 50; round++)
            {
                for (int i = 0; i < 100; i++) dict.Set(Value.FromInt(round * 1000 + i), Value.FromInt(i));
                for (int i = 0; i < 100; i++) Assert.That(dict.Remove(Value.FromInt(round * 1000 + i)), Is.True);
            }
            Assert.That(dict.Count, Is.EqualTo(0));
            dict.Set(Value.FromInt(1), Value.FromInt(1));
            Assert.That(dict.ContainsKey(Value.FromInt(1)), Is.True);
            Assert.That(dict.ContainsKey(Value.FromInt(1001)), Is.False);
        }

        [Test]
        public void ClearEmptiesTheDictionaryAndItCanBeUsedAgain()
        {
            var dict = new PyDict();
            for (int i = 0; i < 50; i++) dict.Set(Value.FromInt(i), Value.FromInt(i));
            dict.Clear();
            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(dict.ContainsKey(Value.FromInt(3)), Is.False);
            dict.Set(Value.FromInt(3), Value.FromInt(33));
            Assert.That(dict.TryGetValue(Value.FromInt(3), out var v) && v.AsInt() == 33, Is.True);
        }

        [Test]
        public void AnUnhashableKeyIsRefusedEvenWhenTheDictionaryIsEmpty()
        {
            var dict = new PyDict();
            var list = Value.FromList(new PyList());
            Assert.Throws<PyError>(() => dict.ContainsKey(Value.FromInt(1) is var one && list.Kind == ValueKind.List ? list : one));
            dict.Set(Value.FromInt(1), Value.None);
            Assert.Throws<PyError>(() => dict.Set(list, Value.None));
        }

        [Test]
        public void AnEqualityMethodThatChangesTheDictionaryDoesNotBreakTheLookup()
        {
            var vm = new PyVm();
            var task = vm.Start(@"
d = {}
once = []
class K:
    def __init__(self, n):
        self.n = n
    def __hash__(self):
        return 7
    def __eq__(self, other):
        if not once:
            once.append(1)
            d[K(1000)] = 0
        return isinstance(other, K) and self.n == other.n
for i in range(5):
    d[K(i)] = i
found = d[K(3)]
extra = K(1000) in d
");
            task.RunUntilDone();
            Assert.That(task.State, Is.EqualTo(TaskState.Finished), task.Error?.ToString());
            Assert.That(task.Globals["found"].AsInt(), Is.EqualTo(3));
            Assert.That(task.Globals["extra"].AsBool(), Is.True);
        }

        [Test]
        public void ASetOfTuplesSurvivesASnapshot()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            var script = vm.Compile("s = set()\nfor i in range(200):\n    s.add((i, i + 1))\ns.remove((5, 6))\nd = {i: str(i) for i in range(30)}\ndel d[3]\nwait = 1\n");
            var task = vm.Start(script);
            task.RunUntilDone();
            var restored = vm.Restore(script, task.Save());
            var s = restored.Globals["s"].AsSet();
            Assert.That(s.Count, Is.EqualTo(199));
            Assert.That(s.Contains(Value.Tuple(Value.FromInt(7), Value.FromInt(8))), Is.True);
            Assert.That(s.Contains(Value.Tuple(Value.FromInt(5), Value.FromInt(6))), Is.False);
            var d = restored.Globals["d"].AsDict();
            Assert.That(d.Count, Is.EqualTo(29));
            Assert.That(d.TryGetValue(Value.FromInt(4), out var four) && four.AsString() == "4", Is.True);
        }

        [Test]
        public void TheMemoryEstimateOfASetMatchesItsRealSize()
        {
            var vm = new PyVm(new VmOptions { MaxSequenceLength = 4194304, MaxMemoryBytes = 0 });
            var task = vm.Start("s = set()\nfor i in range(100000):\n    s.add(i)\n");
            task.RunUntilDone();
            double perItem = task.EstimatedMemoryBytes / 100000.0;
            Assert.That(perItem, Is.InRange(20.0, 28.0), "the members are integers, kept as plain numbers");

            var strings = vm.Start("s = set()\nfor i in range(100000):\n    s.add(str(i) + 'k')\n");
            strings.RunUntilDone();
            Assert.That((strings.EstimatedMemoryBytes - 100000 * (24 + 2 * 7)) / 100000.0, Is.InRange(45.0, 56.0));
        }

        [Test]
        public void RandomOperationsOnIntegerKeysAgreeWithAnOrdinaryDictionary()
        {
            var random = new Random(777);
            var pool = new List<long>();
            for (int i = 0; i < 400; i++) pool.Add(i);
            for (int i = 0; i < 100; i++) pool.Add(-i - 1);
            for (int i = 0; i < 100; i++) pool.Add(((long)i << 33) + 5);
            pool.Add(long.MaxValue); pool.Add(long.MinValue); pool.Add(int.MaxValue); pool.Add((long)int.MaxValue + 1); pool.Add(int.MinValue);
            var dict = new PyDict();
            var model = new Dictionary<long, long>();
            var order = new List<long>();
            for (int step = 0; step < 80000; step++)
            {
                long k = pool[random.Next(pool.Count)];
                switch (random.Next(5))
                {
                    case 0:
                    case 1:
                        dict.Set(Value.FromInt(k), Value.FromInt(step));
                        if (!model.ContainsKey(k)) order.Add(k);
                        model[k] = step;
                        break;
                    case 2:
                        Assert.That(dict.Remove(Value.FromInt(k)), Is.EqualTo(model.Remove(k)));
                        order.Remove(k);
                        break;
                    case 3:
                        Assert.That(dict.ContainsKey(Value.FromInt(k)), Is.EqualTo(model.ContainsKey(k)));
                        break;
                    default:
                        bool found = dict.TryGetValue(Value.FromInt(k), out var v);
                        Assert.That(found, Is.EqualTo(model.ContainsKey(k)));
                        if (found) Assert.That(v.AsInt(), Is.EqualTo(model[k]));
                        break;
                }
                Assert.That(dict.Count, Is.EqualTo(model.Count));
                if (step % 5000 == 0) Assert.That(dict.Pairs().Select(p => p.Key.AsInt()).ToList(), Is.EqualTo(order));
            }
            Assert.That(dict.IntKeyed, Is.True);
            Assert.That(dict.Pairs().Select(p => p.Key.AsInt()).ToList(), Is.EqualTo(order), "insertion order survives removals and compaction");
        }

        [Test]
        public void ADictThatHeldIntegersTurnsIntoAnOrdinaryOneForTheFirstOtherKey()
        {
            var set = new PySet();
            for (int i = 0; i < 100; i++) set.Add(Value.FromInt(i * 3));
            set.Remove(Value.FromInt(6));
            Assert.That(set.Items.IntKeyed, Is.True);
            set.Add(Value.FromString("x"));
            Assert.That(set.Items.IntKeyed, Is.False);
            Assert.That(set.Count, Is.EqualTo(100));
            Assert.That(set.Contains(Value.FromInt(9)), Is.True);
            Assert.That(set.Contains(Value.FromInt(6)), Is.False, "a removed member stays removed");
            Assert.That(set.Contains(Value.FromString("x")), Is.True);
            var members = set.Members().ToList();
            Assert.That(members.Take(3).Select(m => m.AsInt()), Is.EqualTo(new long[] { 0, 3, 9 }));
            Assert.That(members.Last().AsString(), Is.EqualTo("x"));
            set.Add(Value.FromInt(1000));
            Assert.That(set.Contains(Value.FromInt(1000)), Is.True);
        }

        [Test]
        public void LookingUpAnotherKindOfKeyInAnIntegerSetFollowsPythonsRules()
        {
            var vm = new PyVm();
            var task = vm.Start(
                "s = {1, 2, 3}\nd = {1: 'a', 2: 'b'}\n" +
                "r1 = (1.0 in s, True in s, 'a' in s, 2.5 in s)\n" +
                "d[1.0] = 'c'\nd[True] = 'e'\nk = list(d.keys())\nv = list(d.values())\n" +
                "s.add(True)\nn = len(s)\n" +
                "try:\n    [] in s\n    r2 = 'no error'\nexcept TypeError:\n    r2 = 'TypeError'\n" +
                "d[2.5] = 'f'\nk2 = list(d.keys())\n");
            task.RunUntilDone();
            Assert.That(task.Error, Is.Null);
            Assert.That(task.Globals["n"].AsInt(), Is.EqualTo(3));
            Assert.That(task.Globals["r2"].AsString(), Is.EqualTo("TypeError"));
            Assert.That(task.Globals["v"].AsList().Items.Select(x => x.AsString()), Is.EqualTo(new[] { "e", "b" }), "1.0 and True are the key 1");
            Assert.That(task.Globals["k"].AsList().Items.Select(x => x.AsInt()), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(task.Globals["k2"].AsList().Items.Count, Is.EqualTo(3));
            Assert.That(task.Globals["k2"].AsList().Items[2].AsFloat(), Is.EqualTo(2.5));
        }

        [Test]
        public void AnIntegerDictionaryCanBeEmptiedAndReused()
        {
            var dict = new PyDict();
            for (int i = 0; i < 1000; i++) dict.Set(Value.FromInt(i), Value.FromInt(i));
            for (int i = 0; i < 995; i++) dict.Remove(Value.FromInt(i));
            Assert.That(dict.Count, Is.EqualTo(5));
            for (int i = 2000; i < 2100; i++) dict.Set(Value.FromInt(i), Value.None);
            Assert.That(dict.Count, Is.EqualTo(105));
            Assert.That(dict.Pairs().First().Key.AsInt(), Is.EqualTo(995));
            dict.Clear();
            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(dict.IntKeyed, Is.False);
            dict.Set(Value.FromInt(7), Value.FromString("seven"));
            Assert.That(dict.IntKeyed, Is.True);
            Assert.That(dict.TryGetValue(Value.FromInt(7), out var v) && v.AsString() == "seven", Is.True);
        }

        [Test]
        public void AnIntegerSetAndDictSurviveASnapshot()
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = true });
            var script = vm.Compile("s = set()\nfor i in range(500):\n    s.add(i * 5)\ns.remove(25)\nd = {i: i * i for i in range(40)}\ndel d[7]\nwait = 1\n");
            var task = vm.Start(script);
            task.RunUntilDone();
            var restored = vm.Restore(script, task.Save());
            var s = restored.Globals["s"].AsSet();
            Assert.That(s.Count, Is.EqualTo(499));
            Assert.That(s.Contains(Value.FromInt(30)), Is.True);
            Assert.That(s.Contains(Value.FromInt(25)), Is.False);
            s.Add(Value.FromInt(25));
            Assert.That(s.Count, Is.EqualTo(500));
            var d = restored.Globals["d"].AsDict();
            Assert.That(d.Count, Is.EqualTo(39));
            Assert.That(d.TryGetValue(Value.FromInt(9), out var nine) && nine.AsInt() == 81, Is.True);
            Assert.That(d.ContainsKey(Value.FromInt(7)), Is.False);
            Assert.That(restored.Globals["s"].AsSet().Members().Select(m => m.AsInt()).Take(3), Is.EqualTo(new long[] { 0, 5, 10 }));
        }
    }
}
