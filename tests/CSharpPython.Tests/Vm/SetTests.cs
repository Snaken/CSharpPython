using System.Linq;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class SetTests
    {
        private static string Message(string source) => Fails(source).Problem;

        [Test]
        public void SetsKeepInsertionOrder()
        {
            Assert.That(Out("s = {5, 3, 9}\ns.add(1)\ns.add(3)\nprint(s, list(s))"), Is.EqualTo("{5, 3, 9, 1} [5, 3, 9, 1]"));
            Assert.That(Out("s = {5, 3, 9}\ns.discard(3)\ns.add(3)\nprint(s.pop(), s)"), Is.EqualTo("5 {9, 3}"));
        }

        [Test]
        public void EmptySetPrintsAsSetCall()
        {
            Assert.That(Out("print(set(), {1} - {1}, repr(set()))"), Is.EqualTo("set() set() set()"));
        }

        [Test]
        public void UnhashableMembersAreRejected()
        {
            Assert.That(Message("{[1]}"), Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Message("s = set()\ns.add({1: 2})"), Is.EqualTo("unhashable type: 'dict'"));
            Assert.That(Message("[1] in {1}"), Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Message("hash({1})"), Is.EqualTo("unhashable type: 'set'"));
            Assert.That(Message("d = {}\nd[{1}] = 2"), Is.EqualTo("unhashable type: 'set'"));
        }

        [Test]
        public void LookingUpASetNeverFailsAndFindsNothing()
        {
            Assert.That(Out("s = {1}\nprint({1} in s, s.discard({1}), len(s))"), Is.EqualTo("False None 1"));
            Assert.That(Message("{1}.remove({1})"), Is.EqualTo("{1}"));
        }

        [Test]
        public void SetsCannotBeIndexedOrAdded()
        {
            Assert.That(Message("{1}[0]"), Is.EqualTo("'set' object is not subscriptable"));
            Assert.That(Message("{1}[0] = 1"), Is.EqualTo("'set' object does not support item assignment"));
            Assert.That(Message("{1} + {2}"), Is.EqualTo("unsupported operand type(s) for +: 'set' and 'set'"));
            Assert.That(Message("{1} - [1]"), Is.EqualTo("unsupported operand type(s) for -: 'set' and 'list'"));
            Assert.That(Message("{1} < [1]"), Is.EqualTo("'<' not supported between instances of 'set' and 'list'"));
        }

        [Test]
        public void ComparingSetsOnlyOrdersBySubset()
        {
            Assert.That(Out("print({1, 2} < {1, 3}, {1, 2} > {1, 3}, {1, 2} <= {1, 2}, {1, 2} == {1, 3})"), Is.EqualTo("False False True False"));
        }

        [Test]
        public void ReversedAndIndexingBuiltinsRejectSets()
        {
            Assert.That(Message("reversed({1})"), Is.EqualTo("'set' object is not reversible"));
            Assert.That(Message("sorted(5)"), Is.EqualTo("'int' object is not iterable"));
        }

        [Test]
        public void SetOperationsOnlyCopyWhenTheyShould()
        {
            Assert.That(Out("a = {1, 2}\nb = a.copy()\nc = set(a)\nd = a.union()\nb.add(3)\nc.add(4)\nd.add(5)\nprint(a, b, c, d)"),
                Is.EqualTo("{1, 2} {1, 2, 3} {1, 2, 4} {1, 2, 5}"));
            Assert.That(Out("a = {1, 2, 3}\nit = iter(a)\nnext(it)\na.intersection_update({1, 2})\ntry:\n    next(it)\nexcept RuntimeError as e:\n    print(e)"),
                Is.EqualTo("Set changed size during iteration"));
        }

        [Test]
        public void ComprehensionVariablesStayInsideTheComprehension()
        {
            Assert.That(Out("x = 7\ns = {x for x in range(3)}\nprint(x, len(s))"), Is.EqualTo("7 3"));
            Assert.That(Message("s = {y for y in range(3)}\nprint(y)"), Is.EqualTo("name 'y' is not defined"));
        }

        [Test]
        public void SetsWorkInsideEventHandlers()
        {
            var sb = new System.Text.StringBuilder();
            var vm = new PyVm { Output = s => sb.Append(s) };
            var task = vm.Start("seen = set()\ndef on_hit(n):\n    if n in seen:\n        print('again', n)\n    seen.add(n)\n    print(len(seen))\n");
            task.RunUntilDone();
            task.QueueEvent("on_hit", Value.FromInt(1));
            task.QueueEvent("on_hit", Value.FromInt(1));
            task.QueueEvent("on_hit", Value.FromInt(2));
            task.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("1\nagain 1\n1\n2\n"));
        }

        [Test]
        public void HostCanBuildAndReadSets()
        {
            var set = new PySet();
            set.Add(Value.FromInt(1));
            set.Add(Value.FromString("a"));
            var v = Value.FromSet(set);
            Assert.That(v.IsSet, Is.True);
            Assert.That(v.TypeName, Is.EqualTo("set"));
            Assert.That(v.AsSet().Members().ToList(), Is.EqualTo(new[] { Value.FromInt(1), Value.FromString("a") }));
            Assert.That(v.Repr(), Is.EqualTo("{1, 'a'}"));
            Assert.Throws<PyError>(() => Value.FromInt(1).AsSet());

            var vm = new PyVm { Output = s => { } };
            var sb = new System.Text.StringBuilder();
            vm.Output = s => sb.Append(s);
            vm.RegisterModule("game", m => m.Native("tags", (ctx, a) => v, 0, 0));
            vm.Start("import game\nprint(sorted(game.tags(), key=str), 'a' in game.tags())").RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("[1, 'a'] True\n"));
        }

        [Test]
        public void ScriptCanBeAskedWhetherASetIsCallable()
        {
            Assert.That(Out("print(callable(set), callable({1}))"), Is.EqualTo("True False"));
        }
    }
}
