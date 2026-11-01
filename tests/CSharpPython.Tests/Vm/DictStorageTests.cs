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
            Assert.That(perItem, Is.InRange(45.0, 52.0));
        }
    }
}
